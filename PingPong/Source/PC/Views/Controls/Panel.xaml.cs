using System.Windows.Controls;
using System.Windows.Markup;

namespace PingPong {
    /// <summary>
    /// Interaction logic for Panel.xaml
    /// </summary>
    
    [ContentProperty("Children")]
    public partial class Panel : UserControl {
        public Panel() {
            InitializeComponent();
        }
    }
}
