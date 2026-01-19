using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StarResonanceDpsAnalysis.Effects;

namespace StarResonanceDpsAnalysis.Properties
{
    public static class HandledResources
    {
        public const string HARMONY_OS_SANS_FONT_KEY = "HarmonyOS Sans";
        public const string ALIMAMASHUHEITI_FONT_KEY = "阿里妈妈数黑体";
        public const string SAO_WELCOME_TT_FONT_KEY = "SAO Welcome TT";
        public static byte[] FontResource = Resources.HarmonyOS_Sans; // cache
        public static Font GetHarmonyOS_SansFont(float fontSize = 9, FontStyle fontStyle = FontStyle.Regular)
        {
            return FontLoader.LoadFontFromBytesAndCache(HARMONY_OS_SANS_FONT_KEY, FontResource, fontSize, fontStyle);
        }
        public static Font GetHarmonyOS_SansBoldFont(float fontSize = 9, FontStyle fontStyle = FontStyle.Bold)
        {
            return FontLoader.LoadFontFromBytesAndCache(HARMONY_OS_SANS_FONT_KEY, Resources.HarmonyOS_Sans_SC_Bold, fontSize, fontStyle);
        }

        public static Font GetAliMaMaShuHeiTiFont(float fontSize = 9, FontStyle fontStyle = FontStyle.Regular)
        {
            return FontLoader.LoadFontFromBytesAndCache(ALIMAMASHUHEITI_FONT_KEY, Resources.AlimamaShuHeiTi, fontSize, fontStyle);
        }
        public static Font GetSAOWelcomeTTFont(float fontSize = 9, FontStyle fontStyle = FontStyle.Regular)
        {
            return FontLoader.LoadFontFromBytesAndCache(SAO_WELCOME_TT_FONT_KEY, Resources.SAOWelcomeTT, fontSize, fontStyle);
        }
    }
}
