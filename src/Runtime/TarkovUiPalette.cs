using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed class TarkovUiPalette
    {
        public Color Normal,Disabled,Selected,Highlight;
        public Color Text => new Color(Highlight.r,Highlight.g,Highlight.b,1);
        public string Source="";
        public static TarkovUiPalette Read(ReadAccess read)
        {
            // Exact fallback constants from local 4.1.5 .cctors, not a Battlefield color approximation.
            var palette=new TarkovUiPalette {Normal=new Color(0,0,0,1),Disabled=new Color(.25f,.25f,.25f,1),
                Selected=new Color(.514f,.514f,.514f,1),Highlight=new Color32(255,255,255,77),Source="verified SPT 4.1.5 constants"};
            var button=read.FindType("EFT.UI.InteractionButton"); var item=read.FindType("EFT.UI.DragAndDrop.ItemView");
            if(read.Get(button,"NormalColor") is Color normal && read.Get(button,"DisabledColor") is Color disabled &&
                read.Get(button,"SelectedColor") is Color selected && read.Get(item,"DefaultSelectedColor") is Color highlight)
            { palette.Normal=normal; palette.Disabled=disabled; palette.Selected=selected; palette.Highlight=highlight; palette.Source="native InteractionButton / ItemView fields"; }
            return palette;
        }
    }
}
