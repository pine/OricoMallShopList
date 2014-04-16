using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Timers;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace OricoMallShopList
{
    /// <summary>
    /// オリコモールへアクセスし情報を取得するクラス
    /// </summary>
    class OricoMallApi : IDisposable
    {
        private const string OricoMallTopUrl = "http://www.oricomall.com";
        private const string OricoMallShopList = "http://www.oricomall.com/shop_list/indexed/";

        private WebBrowser Browser { get; set; }
        private string UserName { get; set; }
        private string Password { get; set; }
        private List<Shop> ShopInfoLinks { get; set; }
        private List<Shop> ShopLinks { get; set; }
        private int ShopInfoIndex { get; set; }
        private bool TimeoutEnabled { get; set; }
        private Timer TimeoutTimer { get; set; }
        private Timer ReadyStateTimer { get; set; }
        private Action<bool> NextDelegate { get; set; }
        private bool ReadyStateRedirect { get; set; }
        private bool ReadyStateCompleted { get; set; }
        private string ReadyStateUrl { get; set; }

        public EventHandler<string> Failure = (o, s) => { };
        public EventHandler<int> ProgressMaxChanged = (o, v) => { };
        public EventHandler<int> ProgressValueChanged = (o, v) => { };
        public EventHandler<int> LinkLengthChanged = (o, v) => { };
        public EventHandler<List<Shop>> Ended = (o, v) => { };

        public OricoMallApi() {
            this.Browser = null;
            this.UserName = null;
            this.Password = null;
            this.ShopInfoLinks = null;
            this.ShopLinks = null;
            this.ShopInfoIndex = 0;
            this.NextDelegate = null;

            this.TimeoutTimer = new Timer();
            this.TimeoutTimer.AutoReset = false; // 繰り返し無効
            this.TimeoutTimer.Elapsed += this.TimeoutTimer_Elapsed;

            this.ReadyStateTimer = new Timer();
            this.ReadyStateTimer.AutoReset = true;
            this.ReadyStateTimer.Interval = 10;
            this.ReadyStateTimer.Elapsed += this.ReadyStateTimer_Elapsed;
        }

        public void Start(WebBrowser browser, string userName, string password, int timeout)
        {
            this.Browser = browser;
            this.Browser.LoadCompleted += Browser_LoadCompleted;

            this.UserName = userName;
            this.Password = password;
            
            this.ProgressMaxChanged(this, 0);
            this.ProgressValueChanged(this, 0);

            if (timeout > 0) {
                this.TimeoutEnabled = true;
                this.TimeoutTimer.Interval = timeout;
            }
            
            this.Move(OricoMallTopUrl, this.MoveLoginPage);
        }

        private void MoveLoginPage(bool isTimeout)
        {
            // タイムアウト
            if (isTimeout)
            {
                this.Failure(this, "タイムアウトになりました。");
                return;
            }

            // ログインボタンを取得
            var loginButton = this.Document.getElementsByClassName("btnLogin");

            if (loginButton.Count > 0)
            {
                // 未ログインの場合
                if (loginButton[0].innerText == "ログイン")
                {
                    // リンクを取得
                    var link = loginButton[0].getElementsByTagName("a");

                    if (link.Count > 0)
                    {
                        this.Move(null, this.Login, isCompletedTiming: true);
                        link[0].click();
                    }

                    else
                    {
                        // ログインリンク取得失敗
                        this.Failure(this, "ログインリンクの取得に失敗しました");
                    }
                }

                else
                {
                    // ログイン済みの場合
                    this.Move(OricoMallShopList, this.GetShopList, isCompletedTiming: true);
                }
            }
        }

        private void GetShopList(bool isTimeout)
        {
            // タイムアウト
            if (isTimeout)
            {
                this.Failure(this, "タイムアウトになりました。");
                return;
            }

            var shopList = this.Document.getElementById("shop_list");
            var html = this.Document.body.innerHTML;

            if (shopList == null)
            {
                this.Failure(this, "ショップ一覧の取得に失敗しました");
                return;
            }

            var links = shopList.getElementsByTagName("a");
            var enabledLinks = new List<Shop>();

            foreach (var link in links)
            {
                var href = (string)link.getAttribute("href");
                var text = link.innerText;

                if (href != null && href.IndexOf("/shop/") > -1)
                {
                    enabledLinks.Add(new Shop { OricoMallUrl = href, Name = text });
                }
            }

            this.ShopInfoLinks = enabledLinks;
            this.ShopInfoIndex = 0;
            this.ShopLinks = new List<Shop>();

            this.ProgressMaxChanged(this, this.ShopInfoLinks.Count);
            this.ProgressValueChanged(this, 0);

            this.GetShopInfoNext();
        }

        private void GetShopInfoNext()
        {
            // 進行状況を更新
            if (this.ShopInfoIndex > 0)
            {
                this.ProgressValueChanged(this, this.ShopInfoIndex);
            }

            // 終了判定
            if (this.ShopInfoIndex == this.ShopInfoLinks.Count)
            {
                this.End();
                return;
            }

            ++this.ShopInfoIndex;

            this.Move(this.CurrentShop.OricoMallUrl, this.GetShopInfo);
        }

        private void GetShopInfo(bool isTimeout)
        {
            // タイムアウト
            if (isTimeout)
            {
                this.GetShopInfoNext();
                return;
            }

            var linkArea = this.Document.getElementsByClassName("go2shop");

            if (linkArea.Count > 0)
            {
                var link = linkArea[0].getElementsByTagName("a");

                if (link.Count > 0)
                {
                    var url = link[0].getAttribute("href") as string;

                    if (string.IsNullOrEmpty(url))
                    {
                        this.GetShopInfoNext();
                        return;
                    }

                    this.Move(url, this.GetShopUrl, true);
                }

                else
                {
                    this.GetShopInfoNext();
                }
            }

            else
            {
                // 失敗した場合
                this.GetShopInfoNext();
            }
        }

        private void GetShopUrl(bool isTimeout)
        {
            // タイムアウト
            if (isTimeout)
            {
                this.GetShopInfoNext();
                return;
            }
            
            var url = this.Browser.Source.AbsoluteUri;
            this.CurrentShop.Url = this.SanitizeShopUrl(url);
            this.CurrentShop.HostName = this.GetHostName(url);

            this.ShopLinks.Add(this.CurrentShop);
            this.LinkLengthChanged(this, this.ShopLinks.Count);
            
            this.GetShopInfoNext();
        }

        private void Login(bool isTimeout)
        {
            // タイムアウト
            if (isTimeout)
            {
                this.Failure(this, "タイムアウトになりました。");
                return;
            }

            var loginId = this.Document.getElementById("loginId");
            var password = this.Document.getElementById("password");
            var captcha = this.Document.getElementById("captchaString");

            if (loginId != null && password != null && captcha != null)
            {
                loginId.setAttribute("value", this.UserName);
                password.setAttribute("value", this.Password);

                // 日本語の画像認証なため、日本語入力を ON にする
                captcha.focus();

                var loginButton = this.Document.getElementById("connectLogin");

                captcha.attachEvent("onkeypress", (args) =>
                {
                    const int ENTER_KEY = 13;

                    var e = args[0] as mshtml.IHTMLEventObj;

                    if (e != null && e.keyCode == ENTER_KEY)
                    {
                        loginButton.click();
                    }

                    return true;
                });

                // ログイン後の処理
                this.Move(this.LoggedIn);
            }

            else
            {
              //  this.Failure(this, "ログインに失敗しました。");
            }
        }

        private void LoggedIn(bool isTimeout)
        {
            // タイムアウト
            if (isTimeout)
            {
                this.Failure(this, "タイムアウトになりました。");
                return;
            }

            var loginButtton = this.Document.getElementsByClassName("btn-em-01");

            // ログインが完了していない場合
            if (loginButtton.Count > 0)
            {
                this.Login(isTimeout);
                return;
            }

            this.Move(OricoMallShopList, this.GetShopList);
        }

        private void End()
        {
            this.Ended(this, this.ShopLinks);
        }

        /// <summary>
        /// タイムアウトの処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TimeoutTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.Browser.Dispatcher.Invoke(() =>
            {
                if (this.NextDelegate != null)
                {
                    var next = this.NextDelegate;
                    this.NextDelegate = null;

                    next(true);
                }
            });
        }

        /// <summary>
        /// ロード状態監視タイマー
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ReadyStateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                this.Browser.Dispatcher.Invoke(() =>
                {
                    // 別スレッド実行なため、タイマーが既に停止していないか確認する
                    if (this.ReadyStateTimer.Enabled)
                    {
                        var document = this.Browser.Document as mshtml.HTMLDocument;

                        if (document != null)
                        {
                            Debug.WriteLine(document.readyState);

                            // リダイレクトが有効な場合は、リダイレクトが完了しているか調べる
                            if ((document.readyState == "interactive" || document.readyState == "complete") &&
                                document.body != null &&
                                this.CheckRedirectEnded())
                            {
                                this.ReadyStateTimer.Stop();

                                if (this.NextDelegate != null && !this.ReadyStateCompleted)
                                {
                                    // 一度変数に格納しないと null が代入できない
                                    // デリゲートを呼び出してから null 代入すると、
                                    // デリゲート中から次のロード処理が実行し、正常に動作しない
                                    var next = this.NextDelegate;
                                    this.NextDelegate = null;

                                    next(false); // 処理呼び出し
                                }
                            }
                        }
                    }
                });
            }
            catch (TaskCanceledException) { } // 急にタスクを中断するときの発生する例外
        }

        private bool CheckRedirectEnded()
        {
            if (this.ReadyStateRedirect)
            {
                var RedirectCheckUrl = "http://www.oricomall.com";

                if (this.Browser.Source != null)
                {
                    var url = this.Browser.Source.AbsoluteUri;

                    if (!url.StartsWith(RedirectCheckUrl))
                    {
                        return true;
                    }
                }

                return false;
            }
            else
            {
                return true;
            }
        }

        private void Browser_LoadCompleted(object sender, NavigationEventArgs e)
        {
            if (this.ReadyStateUrl == null ||
                this.Browser.Source.AbsoluteUri == this.ReadyStateUrl)
            {
                if (this.NextDelegate != null)
                {
                    var next = this.NextDelegate;
                    this.NextDelegate = null;

                    next(false);
                }
            }                 
        }

        private mshtml.HTMLDocument Document
        {
            get
            {
                return (mshtml.HTMLDocument)this.Browser.Document;
            }
        }

        private Shop CurrentShop
        {
            get
            {
                return this.ShopInfoLinks[this.ShopInfoIndex - 1];
            }
        }

        private void Move(string url, Action<bool> next, bool isRedirect = false, bool isCompletedTiming = false)
        {
            // 移動後に実行する処理
            this.NextDelegate = next;
            this.ReadyStateRedirect = isRedirect;
            this.ReadyStateCompleted = isCompletedTiming;
            this.ReadyStateUrl = url;
            
            // タイムアウトが有効な場合
            if (this.TimeoutEnabled)
            {
                this.TimeoutTimer.Start();
            }

            // 移動開始
            if (!string.IsNullOrEmpty(url))
            {
                this.Browser.Navigate(url);
            }

            // 文書読み込みを待機
            if (!isCompletedTiming)
            {
                this.ReadyStateTimer.Start();
            }
        }

        private void Move(Action<bool> handler)
        {
            this.Move(null, handler);
        }


        private string SanitizeShopUrl(string url)
        {
            Uri uri = new Uri(url);

            return uri.GetLeftPart(UriPartial.Path);
        }

        private string GetHostName(string url)
        {
            Uri uri = new Uri(url);

            return uri.Host;
        }

        
        #region Dispose Finalize パターン

        /// <summary>
        /// 既にDisposeメソッドが呼び出されているかどうかを表します。
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// ConsoleApplication1.DisposableClass1 によって使用されているすべてのリソースを解放します。
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            this.Dispose(true);
        }

        /// <summary>
        /// ConsoleApplication1.DisposableClass1 クラスのインスタンスがGCに回収される時に呼び出されます。
        /// </summary>
        ~OricoMallApi()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// ConsoleApplication1.DisposableClass1 によって使用されているアンマネージ リソースを解放し、オプションでマネージ リソースも解放します。
        /// </summary>
        /// <param name="disposing">マネージ リソースとアンマネージ リソースの両方を解放する場合は true。アンマネージ リソースだけを解放する場合は false。 </param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }
            this.disposed = true;

            if (disposing)
            {
                // マネージ リソースの解放処理をこの位置に記述します。
                if (this.TimeoutTimer != null) { this.TimeoutTimer.Stop(); }
                if (this.ReadyStateTimer != null) { this.ReadyStateTimer.Stop(); }
            }
            // アンマネージ リソースの解放処理をこの位置に記述します。
        }

        /// <summary>
        /// 既にDisposeメソッドが呼び出されている場合、例外をスローします。
        /// </summary>
        /// <exception cref="System.ObjectDisposedException">既にDisposeメソッドが呼び出されています。</exception>
        protected void ThrowExceptionIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }

        /// <summary>
        /// Dispose Finalize パターンに必要な初期化処理を行います。
        /// </summary>
        private void InitializeDisposeFinalizePattern()
        {
            this.disposed = false;
        }

        #endregion
    }
}
