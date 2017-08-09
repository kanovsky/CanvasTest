using System;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Linq;

namespace Pear.RiaServices.Server
{
    public partial class TimeSlice
    {
        public string Blocation;

        public string BookingNo;

        public DateTime? End;
        public DateTime? Enter;
        public DateTime? Exit;
        public DateTime? Expiration;
        
        public string CharacterOfSale { get; set; }
        public bool IsPresent { get; set; }
        public Facility ParentFacility { get; set; }
        public string PaymentStatus { get; set; }
        public TimeSliceState Status { get; set; }
        public string SurfaceDescr { get; set; }

        public bool IsSelected { get; set; }

        public string Persons { get; set; }
        public string PersonRows
        {
            get 
            {
                var arr = Persons.Split(',').Select(s=>s.Trim());
                var rows = string.Join(Environment.NewLine, arr);

                return rows; 
            }
        }

        public Polyline GuiElement { get; set; }
        public TextBlock GuiDescr { get; set; }

        public Action RemoveGuiFromVisualTree { get; set; }
        public Action SetGuiInVisualTree { get; set; }
        
        public string ColorName
        {
            get 
            {
                if (!string.IsNullOrEmpty(Blocation))
                    return "MagentaBackground";
                if (Status == TimeSliceState.available)
                    return "GreyBackground";
                else if (Status == TimeSliceState.sold)
                    return PaymentStatus == "DOPLATEK" ? "PinkBackground" : "RedBackground";
                else if (Status == TimeSliceState.reserved)
                    return 
                        PaymentStatus == "FAKTURA" ? "GreenBackground" :
                        (Expiration.HasValue && Expiration < DateTime.Now) ? "BlueBackground" :
                        "LightGreenBackground";

                return "GreyBackground"; 
            }
        }
    }

    public enum TimeSliceState
    {
        undefined = 0,
        available = 1,
        reserved = 2,
        sold = 3,
    }
}
