namespace Tylevo.FieldAttachments.Runtime
{
    public static class NativeSprintReader
    {
        // SPT 4.1.5 MovementContext.IsSprintEnabled reads this exact property.
        // PhysicalBase.Sprinting is a field-backed getter with no side effects.
        // Animation flags may be absent on weapon controllers; never infer false.
        public static bool? Read(ReadAccess read, object? player) =>
            ReadAccess.Bool(read.Get(read.Get(player, "Physical"), "Sprinting"));
    }
}
