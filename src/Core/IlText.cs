using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Tylevo.FieldAttachments.Core
{
    // Metadata/IL reading only; does not invoke the exported game methods.
    public static class IlText
    {
        private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
            .GroupBy(c => c.Value).ToDictionary(g => g.Key, g => g.First());

        public static string Signature(MethodBase m)
        {
            string prefix = m is MethodInfo mi ? TypeName(mi.ReturnType) + " " : "";
            string args = string.Join(", ", m.GetParameters().Select(p =>
                (p.IsOut ? "out " : "") + TypeName(p.ParameterType) + " " + p.Name + (p.IsOptional ? " = [optional]" : "")));
            return (m.IsStatic ? "static " : "instance ") + prefix + m.DeclaringType?.FullName + "::" + m.Name + (m.IsGenericMethod ? "<" + string.Join(",", m.GetGenericArguments().Select(TypeName)) + ">" : "") + "(" + args + ")";
        }
        public static string TypeName(Type t)
        {
            if (t.IsByRef) return TypeName(t.GetElementType()!) + "&";
            if (t.IsPointer) return TypeName(t.GetElementType()!) + "*";
            if (t.IsArray) return TypeName(t.GetElementType()!) + "[" + new string(',', t.GetArrayRank() - 1) + "]";
            if (t.IsGenericParameter) return t.Name;
            if (!t.IsGenericType) return t.FullName ?? t.Name;
            string name = t.GetGenericTypeDefinition().FullName ?? t.Name;
            name = System.Text.RegularExpressions.Regex.Replace(name, @"`\d+", "");
            return name + "<" + string.Join(",", t.GetGenericArguments().Select(TypeName)) + ">";
        }

        public static string Dump(MethodBase method, int maxInstructions = 600, ICollection<MethodBase>? references = null)
        {
            var b = new StringBuilder();
            try
            {
                MethodBody? body = method.GetMethodBody();
                byte[]? bytes = body?.GetILAsByteArray();
                if (bytes == null) return "  [No IL body available]\n";
                if (bytes.Length > 65536) return "  [Body exceeds export cap]\n";
                b.AppendLine("  BODY bytes=" + bytes.Length + "; initLocals=" + body!.InitLocals);
                foreach (LocalVariableInfo local in body.LocalVariables.Take(128))
                    b.AppendLine("  LOCAL " + local.LocalIndex + " " + TypeName(local.LocalType) + (local.IsPinned ? " pinned" : ""));
                foreach (ExceptionHandlingClause clause in body.ExceptionHandlingClauses.Take(64))
                {
                    string detail = clause.Flags == ExceptionHandlingClauseOptions.Clause ? " catch=" + TypeName(clause.CatchType!) :
                        clause.Flags == ExceptionHandlingClauseOptions.Filter ? " filter=IL_" + clause.FilterOffset.ToString("x4") : "";
                    b.AppendLine("  EH " + clause.Flags + " try=[IL_" + clause.TryOffset.ToString("x4") + ",IL_" +
                        (clause.TryOffset + clause.TryLength).ToString("x4") + ") handler=[IL_" + clause.HandlerOffset.ToString("x4") +
                        ",IL_" + (clause.HandlerOffset + clause.HandlerLength).ToString("x4") + ")" + detail);
                }
                int p = 0, count = 0;
                while (p < bytes.Length && count++ < maxInstructions)
                {
                    int at = p;
                    short code = bytes[p++];
                    if (code == 0xfe) { Need(bytes, p, 1); code = unchecked((short)(0xfe00 | bytes[p++])); }
                    if (!Codes.TryGetValue(code, out OpCode op)) { b.AppendLine("  [Unknown opcode]"); break; }
                    b.Append("  IL_").Append(at.ToString("x4")).Append(": ").Append(op.Name);
                    string operand = "";
                    switch (op.OperandType)
                    {
                        case OperandType.InlineNone: break;
                        case OperandType.ShortInlineI: Need(bytes, p, 1); operand = unchecked((sbyte)bytes[p++]).ToString(); break;
                        case OperandType.ShortInlineVar: Need(bytes, p, 1); operand = bytes[p++].ToString(); break;
                        case OperandType.InlineVar: Need(bytes, p, 2); operand = BitConverter.ToUInt16(bytes, p).ToString(); p += 2; break;
                        case OperandType.InlineI: operand = ReadInt(bytes, ref p).ToString(); break;
                        case OperandType.InlineI8: Need(bytes, p, 8); operand = BitConverter.ToInt64(bytes, p).ToString(); p += 8; break;
                        case OperandType.ShortInlineR: Need(bytes, p, 4); operand = BitConverter.ToSingle(bytes, p).ToString(System.Globalization.CultureInfo.InvariantCulture); p += 4; break;
                        case OperandType.InlineR: Need(bytes, p, 8); operand = BitConverter.ToDouble(bytes, p).ToString(System.Globalization.CultureInfo.InvariantCulture); p += 8; break;
                        case OperandType.ShortInlineBrTarget:
                            Need(bytes, p, 1); int delta = unchecked((sbyte)bytes[p++]); operand = "IL_" + (p + delta).ToString("x4"); break;
                        case OperandType.InlineBrTarget:
                            int jump = ReadInt(bytes, ref p); operand = "IL_" + (p + jump).ToString("x4"); break;
                        case OperandType.InlineSwitch:
                            int n = ReadInt(bytes, ref p);
                            if (n < 0 || n > 2048) throw new InvalidOperationException("Switch exceeds export cap");
                            Need(bytes, p, n * 4); int end = p + n * 4;
                            var targets = new string[n];
                            for (int i = 0; i < n; i++) targets[i] = "IL_" + (end + ReadInt(bytes, ref p)).ToString("x4");
                            operand = string.Join(", ", targets); break;
                        case OperandType.InlineString:
                            int st = ReadInt(bytes, ref p);
                            try { string s = method.Module.ResolveString(st); operand = JsonText.Quote(s.Length > 160 ? s.Substring(0, 160) + "..." : s); }
                            catch { operand = "string-token 0x" + st.ToString("x8"); }
                            break;
                        case OperandType.InlineField:
                        case OperandType.InlineMethod:
                        case OperandType.InlineType:
                        case OperandType.InlineTok:
                            int token = ReadInt(bytes, ref p); operand = Resolve(method, token, references); break;
                        case OperandType.InlineSig:
                            operand = "signature-token 0x" + ReadInt(bytes, ref p).ToString("x8"); break;
                        default: throw new NotSupportedException("Unsupported operand " + op.OperandType);
                    }
                    if (operand.Length > 0) b.Append(' ').Append(operand);
                    b.AppendLine();
                }
                if (p < bytes.Length) b.AppendLine("  [Instruction cap reached; body is partial]");
            }
            catch (Exception e) { b.AppendLine("  [IL export unavailable: " + e.GetType().Name + "]"); }
            return b.ToString();
        }
        // CustomAttributeData reads metadata without running the attribute's constructor.
        public static List<MethodBase> StateMachineBodies(MethodBase method)
        {
            var result = new List<MethodBase>();
            try
            {
                foreach (CustomAttributeData data in method.GetCustomAttributesData())
                {
                    string? name = data.AttributeType.FullName;
                    if (name != "System.Runtime.CompilerServices.AsyncStateMachineAttribute" &&
                        name != "System.Runtime.CompilerServices.IteratorStateMachineAttribute") continue;
                    if (data.ConstructorArguments.Count != 1 || !(data.ConstructorArguments[0].Value is Type state)) continue;
                    foreach (MethodInfo member in state.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                        if (member.Name == "MoveNext" || member.Name == "System.Collections.IEnumerator.MoveNext") result.Add(member);
                }
            }
            catch { /* Runtime/AOT metadata may be unavailable; the caller still reports the wrapper. */ }
            return result;
        }
        private static string Resolve(MethodBase method, int token, ICollection<MethodBase>? references)
        {
            try
            {
                MemberInfo? member = method.Module.ResolveMember(token, method.DeclaringType?.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null);
                if (member is MethodBase mb) { references?.Add(mb); return Signature(mb); }
                return member is Type t ? TypeName(t) : member?.DeclaringType?.FullName + "::" + member;
            }
            catch { return "unresolved-token 0x" + token.ToString("x8"); }
        }
        private static int ReadInt(byte[] b, ref int p) { Need(b, p, 4); int n = BitConverter.ToInt32(b, p); p += 4; return n; }
        private static void Need(byte[] b, int p, int length) { if (length < 0 || p < 0 || p > b.Length - length) throw new InvalidOperationException("Truncated IL"); }
    }
}
