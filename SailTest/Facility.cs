using System.Collections.Generic;
using System.Windows.Media;

namespace Pear.RiaServices.Server
{
    public partial class Facility
    {
        public bool IsExtraBed
        {
            get { return Position.EndsWith("_P"); }
        }

        private static Color Red = Color.FromArgb(255, 0, 0, 0);
        private static Color Green = Color.FromArgb(255, 0, 0, 0);
        private static Color Blue = Color.FromArgb(255, 198, 198, 198);

        public static Brush GetStroke(Facility fa)
        {
            Color color_ceiling = Red;
            Color color_floor = Green;
            
            return 
                new LinearGradientBrush(
                    new GradientStopCollection() 
                    { 
                        new GradientStop () { Color = color_ceiling, Offset = 0},
                        new GradientStop () { Color = color_floor, Offset = 1} 
                    }, 
                    90);
        }

        public IEnumerable<TimeSlice> TimeSliceList { get; set; }

        public string Group { get; set; }
        public string Position { get; set; }
        public string Type { get; set; }
    }

    public enum GroupType
    {
        is_ceiling,
        is_middle,
        is_floor,
        is_single
    }
}
