using System.Collections.ObjectModel;
using System.ComponentModel;

namespace YMTEditor
{
    public class PropData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string propType;
        public int propAnchorId;
        private ObservableCollection<PropDrawable> _propList;
        public ObservableCollection<PropDrawable> propList
        {
            get { return _propList; }
            set
            {
                _propList = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("propList"));
            }
        }

        public string propHeader { get; set; }
        public string slotLabel { get; set; } //what this slot holds, e.g. p_eyes -> "glasses"

        public PropData(string type, int id, ObservableCollection<PropDrawable> props)
        {
            propType = type;
            propAnchorId = id;
            propList = props;
            propHeader = type.ToUpper();
            slotLabel = YMTTypes.SlotLabel(type);
        }
    }
}
