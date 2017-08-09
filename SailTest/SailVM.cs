using Pear.RiaServices.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Pear.RiaServices.Client.DataComponent
{
    public class SailVM : INotifyPropertyChanged
    {
        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private void RaisePropertyChanged(params string[] propertyNames)
        {
            foreach (var pn in propertyNames)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(pn));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #region Properties
        
        public Action UIDialogCloseAction { get; set; }
        public Action<List<Facility>> DrawNewSail;
        public Action<List<Facility>> DrawFilteredSail;
        
        private List<Facility> m_Sail;
        public List<Facility> Sail
        {
            get
            {
                if (m_Sail == null)
                    m_Sail = Enumerable.Empty<Facility>().ToList();

                return m_Sail;
            }
            set
            {
                m_Sail = value;
                RaisePropertyChanged("Sail");
            }
        }

        private DateTime m_SailBegin;
        public DateTime SailBegin
        {
            get 
            {
                if (m_SailBegin == new DateTime())
                {
                    var fstInMonth = DateTime.Today.AddDays(-DateTime.Today.Day + 1);
                    m_SailBegin = fstInMonth.AddMonths(-2);   
                }
                return m_SailBegin; 
            }
            set
            {
                m_SailBegin = value;
                RaisePropertyChanged("SailBegin");
            }
        }
        private DateTime m_SailEnd;
        public DateTime SailEnd
        {
            get 
            {
                if (m_SailEnd == new DateTime())
                {
                    var fstInMonth = DateTime.Today.AddDays(-DateTime.Today.Day + 1);
                    m_SailEnd = fstInMonth.AddMonths(1).AddDays(-1);
                }
                return m_SailEnd; 
            }
            set
            {
                m_SailEnd = value;
                RaisePropertyChanged("SailEnd");
            }
        }
        
        private IEnumerable<Facility> m_SelectedFacilities;
        public IEnumerable<Facility> SelectedFacilities
        {
            get 
            {
                if (m_SelectedFacilities == null)
                    m_SelectedFacilities = Enumerable.Empty<Facility>();

                return m_SelectedFacilities; 
            }
            set
            {
                m_SelectedFacilities = value;
                
                RaisePropertyChanged("AnyAreaSelected");
            }
        }
        
        #endregion
        
        public SailVM()
        {
            var start = DateTime.Today.AddDays(-DateTime.Today.Day).AddMonths(-2);
            var rnd = new Random();

            Sail =
                (from i in Enumerable.Range(1, 80)
                 select new Facility
                 {
                     Group = (i % 2).ToString(),
                     Position = (i % 3).ToString(),
                     Type = i.ToString(),
                     TimeSliceList = 
                        from j in Enumerable.Range(0, 12)
                        let enter = start.AddDays(j * 7)
                        select new TimeSlice
                        {
                            Enter = enter,
                            End = enter.AddDays(6),
                            Exit = enter.AddDays(6),
                            BookingNo = $"{i}/{j}",
                            Status = (TimeSliceState)(rnd.Next() % 4)
                        }
                 })
                .ToList();
        }
        
        public void Redraw()
        {
            DrawNewSail?.Invoke(Sail);
        }
    }
}
