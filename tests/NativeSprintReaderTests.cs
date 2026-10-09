using System;
using Tylevo.FieldAttachments.Runtime;

internal static partial class Program
{
    private sealed class SprintPlayerFixture
    {
        public object? Physical;
        public SprintPlayerFixture(object? physical) { Physical = physical; }
    }
    private sealed class SprintPhysicalFixture { public bool Sprinting { get; set; } }
    private sealed class WrongSprintFixture { public string Sprinting => "False"; }
    private sealed class BrokenSprintFixture { public bool Sprinting => throw new InvalidOperationException("fixture"); }

    private static void TestNativeSprintReader()
    {
        var read = new ReadAccess();
        var physical = new SprintPhysicalFixture();
        var player = new SprintPlayerFixture(physical);
        Check(NativeSprintReader.Read(read, player) == false, "Stopped player is readable without any weapon animator or BOOL_SPRINT parameter");
        physical.Sprinting = true;
        Check(NativeSprintReader.Read(read, player) == true, "Actual native sprint state is resampled on the same player");
        physical.Sprinting = false;
        Check(NativeSprintReader.Read(read, player) == false, "Stopping sprint is observed without rebuilding or saving pose markers");
        player.Physical = new SprintPhysicalFixture { Sprinting = true };
        Check(NativeSprintReader.Read(read, player) == true, "Replaced physical state does not reuse a stale sprint value");
        Check(NativeSprintReader.Read(read, null) == null, "Missing player is unknown rather than stopped");
        player.Physical = null;
        Check(NativeSprintReader.Read(read, player) == null, "Missing physical state stays unknown");
        Check(NativeSprintReader.Read(read, new object()) == null, "Unknown player contract cannot authorize a pose");
        player.Physical = new WrongSprintFixture();
        Check(NativeSprintReader.Read(read, player) == null, "Non-boolean sprint values are not coerced or guessed");
        player.Physical = new BrokenSprintFixture();
        Check(NativeSprintReader.Read(read, player) == null, "Failed sprint getter preserves the unknown-state guard");
    }
}
