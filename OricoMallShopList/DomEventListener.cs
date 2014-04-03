using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace OricoMallShopList
{
    /// <summary>
    /// EventHandler - adaptor to call C# back from JavaScript or DOM event handlers
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDispatch)]
    public class DomEventHandler
    {
        [ComVisible(false)]
        public delegate void Callback(object[] args);

        [ComVisible(false)]
        private Callback callback;
        
        [DispId(0)]
        public object Method(params object[] args)
        {
            callback(args);
            return Type.Missing; // Type.Missing is "undefined" in JavaScript
        }

        public DomEventHandler(Callback callback)
        {
            this.callback = callback;
        }
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDispatch)]
    public class DomEventHandler<T>
    {
        [ComVisible(false)]
        public delegate T Callback(object[] args);

        [ComVisible(false)]
        private Callback callback;

        [DispId(0)]
        public object Method(params object[] args)
        {
            return (object)callback(args);
        }

        public DomEventHandler(Callback callback)
        {
            this.callback = callback;
        }
    }
}
