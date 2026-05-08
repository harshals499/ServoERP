using System;
using System.Drawing;

namespace HVAC_Pro_Desktop.Models
{
    public sealed class CardLayoutDto
    {
        public string PageKey { get; set; }
        public string CardKey { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string SizePreset { get; set; }
        public DateTime SavedAt { get; set; }
    }

    public sealed class CardDefaultSize
    {
        public Size Size { get; set; }
        public string SizePreset { get; set; }
    }
}
