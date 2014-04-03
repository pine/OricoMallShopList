using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace OricoMallShopList
{
    public interface IMainWindow
    {
        WebBrowser WebBrowser
        {
            get;
        }

        PasswordBox PasswordBox
        {
            get;
        }
    }
}
