using System.ComponentModel;
using System.Windows.Documents;
using System.Windows.Media;

namespace Taiji.GUI
{
    /// <summary>左侧大纲项：点击跳转到对应消息块。</summary>
    public sealed class OutlineItem : INotifyPropertyChanged
    {
        private string _preview;

        public OutlineItem(string roleLabel, string preview, Block anchor, Brush accent)
        {
            RoleLabel = roleLabel ?? "";
            _preview = preview ?? "";
            Anchor = anchor;
            Accent = accent;
        }

        public string RoleLabel { get; private set; }
        public Block Anchor { get; private set; }
        public Brush Accent { get; private set; }

        public string Preview
        {
            get { return _preview; }
            set
            {
                if (_preview == value) return;
                _preview = value ?? "";
                var h = PropertyChanged;
                if (h != null) h(this, new PropertyChangedEventArgs("Preview"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
