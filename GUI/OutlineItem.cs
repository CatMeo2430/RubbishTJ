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
            get => _preview;
            set
            {
                if (_preview == value) return;
                _preview = value ?? "";
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Preview)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
