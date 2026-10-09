using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestPoseResume()
    {
        var f=new ClickFixture(); var snapshot=f.Snapshot(); var hands=new object();
        var resume=new PoseResume(); var native=new InstallSession();
        void Begin(bool mode=true)
        {
            native=new InstallSession(); resume.Track(snapshot,hands,native.Revision,mode);
            native.TryBegin(); native.MarkSubmitted();
        }
        bool Poll(bool ready=true,bool mode=true,bool interrupted=false,bool defer=false,double now=1,bool visibleIdle=true) =>
            resume.Poll(native,snapshot,hands,mode,interrupted,ready,visibleIdle,defer,now);
        foreach(var action in new[] {AttachmentAction.Install,AttachmentAction.Uninstall})
        {
            Begin();
            Check(!Poll() && resume.Pending,action+" cannot resume while native transaction is pending");
            native.Finish("same real item verified",false,true);
            Check(!Poll(false) && resume.Pending,action+" success waits for hands readiness");
            Check(!Poll(),action+" requires more than one idle observation");
            Check(Poll() && !resume.Pending,action+" resumes once after success and stable idle");
            Check(!Poll(),action+" cannot repeat inspection after consuming the attempt");
            Begin(); native.Finish("same real item verified",false,true);
            for(int frame=0;frame<12;frame++)
                Check(!Poll(visibleIdle:false) && resume.Pending,action+" native Idling before visual idle preserves the single attempt");
            Check(!Poll() && resume.Pending,"First visible idle sample does not start inspection");
            Check(!Poll(visibleIdle:false) && resume.Pending,"Visual transition resets stable sample count");
            Check(!Poll() && Poll() && !Poll(),action+" stable visible idle consumes exactly one attempt");
        }
        Begin(false); native.Finish("success",false,true);
        Check(!Poll() && !resume.Pending,"F8-only actions do not start attachment inspection");
        Begin(); native.Finish("success",false,true); Poll(); Poll(false);
        Check(!Poll() && Poll(),"Busy hands reset the consecutive idle observations");
        Begin(); native.Finish("success",false,true); Poll();
        Check(!Poll(defer:true) && resume.Pending && Poll(),"F10 report frame defers inspection without losing the request");
        Begin(); native.Finish("success",false,true); Poll(false,now:10);
        Check(!Poll(now:18) && !resume.Pending && !Poll(now:19),"Idle timeout cancels permanently without retry");
        Begin(); native.Finish("native rejected",false);
        Check(!Poll() && !resume.Pending,"Rejected native move does not restart inspection");
        Begin(); native.TimedOut();
        Check(!Poll() && !resume.Pending,"UNKNOWN cancels pose resume even while native task is still pending");
        native.Finish("late success",false,true);
        Check(!Poll(),"Late native completion cannot resurrect cancelled pose resume");
        Begin(); Check(!Poll(mode:false) && !resume.Pending,"Closing pose mode cancels during native pending");
        native.Finish("success",false,true);
        Check(!Poll() && !resume.Pending,"Reopening cannot resume an old transaction");
        Begin(); Check(!Poll(interrupted:true) && !resume.Pending,"Sprint or gameplay interruption cancels during native pending");
        native.Finish("success",false,true); Check(!Poll(),"Stopping sprint does not reactivate a cancelled resume");
        Begin(); native.Finish("success",false,true); Poll();
        Check(!Poll(interrupted:true) && !resume.Pending,"Interruption wins on the frame that idle becomes ready");
        foreach(string change in new[] {"player","weapon","controller","hands","weapon id"})
        {
            Begin(); native.Finish("success",false,true);
            var changed=f.Snapshot(); object currentHands=hands;
            switch(change)
            {
                case "player": changed.Player=new object(); break;
                case "weapon": changed.Weapon=new object(); break;
                case "controller": changed.Controller=new object(); break;
                case "hands": currentHands=new object(); break;
                case "weapon id": changed.WeaponId="other"; break;
            }
            Check(!resume.Poll(native,changed,currentHands,true,false,true,true,false,1) && !resume.Pending,
                "Pose resume rejects changed "+change);
        }
        Begin(); native.Finish("old success",false,true);
        resume.Track(snapshot,hands,native.Revision,true);
        Check(!Poll() && !resume.Pending,"Previous native success cannot authorize a new pose resume");
        native=new InstallSession(); resume.Track(snapshot,hands,native.Revision,true);
        native.TryBegin(); native.Finish("unsubmitted validation result",false,true);
        Check(!Poll() && !resume.Pending,"An unsubmitted request cannot resume inspection");
        Begin(); resume.Cancel("closed"); native.Finish("success",false,true);
        Check(!Poll() && !resume.Pending,"Explicit close clears identities and ignores later completion");
        native=new InstallSession(); resume.Track(snapshot,null,native.Revision,true);
        Check(!resume.Pending,"Missing hands identity cannot schedule an inspection");
        Begin(); native.Finish("success",false,true); Poll(visibleIdle:false,now:10);
        Check(!Poll(visibleIdle:false,now:18) && !resume.Pending,"Visual idle timeout remains bounded");
        Check(!Poll(now:19),"Late visible idle cannot resurrect timed-out resume");
        Begin(); native.Finish("success",false,true); Poll(visibleIdle:false);
        Check(!Poll(mode:false) && !Poll(),"Close while visual return is pending prevents reopening");
    }
}
