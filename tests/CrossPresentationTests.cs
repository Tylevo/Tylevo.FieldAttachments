using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestCrossPresentation()
    {
        // Replay both explicit attachment actions through the observed idle gap and resume.
        // Held is deliberately still true on the click frame: pending action must win.
        foreach (string action in new[] { "install", "uninstall" })
        {
            Check(CrossPresentation.HiddenReason(true,true,true,false,false,false)=="", action+": held cards available before click");
            Check(CrossPresentation.HiddenReason(true,true,true,true,false,false)!="", action+": hide immediately at confirmed click");
            Check(CrossPresentation.HiddenReason(true,true,false,true,false,false)!="", action+": authored lowering stays hidden");
            Check(CrossPresentation.HiddenReason(true,false,false,false,true,true)!="", action+": native transaction at shooting idle stays hidden");
            Check(CrossPresentation.HiddenReason(true,false,false,false,false,true)!="", action+": successful result before visible idle never flashes cards");
            Check(CrossPresentation.HiddenReason(true,false,false,false,false,false)!="", action+": consumed resume before Start has no display gap");
            Check(CrossPresentation.HiddenReason(true,true,false,false,false,false)!="", action+": resumed entrance stays hidden");
            Check(CrossPresentation.HiddenReason(true,true,true,false,false,false)=="", action+": cards return when held again");
        }
        Check(CrossPresentation.HiddenReason(true,false,false,false,false,false)!="", "Rejected/unknown pose resume leaves requested pose cards hidden at idle");
        Check(CrossPresentation.HiddenReason(false,true,false,false,false,false)!="", "Close while F8 remains open hides the entire returning pose");
        Check(CrossPresentation.HiddenReason(false,false,false,false,false,false)=="", "Standalone F8 inspection-free browsing remains visible");
        Check(CrossPresentation.HiddenReason(false,false,false,true,false,false)!="", "Standalone F8 click waiting also hides cards");
        Check(CrossPresentation.HiddenReason(false,false,false,false,true,false)!="", "Standalone F8 native change hides cards even without pose resume");
        Check(CrossPresentation.HiddenReason(false,false,false,false,false,true)!="", "Pending resume cannot expose orphan cards after pose closure");
        Check(CrossPresentation.HiddenReason(true,true,false,false,false,false)!="", "First O entrance has no floating unbound cross");
        Check(CrossPresentation.HiddenReason(false,true,true,false,false,false)=="", "A manually marked held pose can still show its cards");
    }
}
