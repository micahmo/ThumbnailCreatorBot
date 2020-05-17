using mdm.Extensions;
using SixLabors.Fonts;

namespace ThumbnailCreatorBot
{
    public static class Utilities
    {
        public static IReadOnlyFontCollection Fonts
        {
            get
            {
                if (_fonts.IsNull())
                {
                    _fonts = SystemFonts.Collection;
                }

                return _fonts;
            }
            set => _fonts = value;
        }

        private static IReadOnlyFontCollection _fonts;
    }
}
