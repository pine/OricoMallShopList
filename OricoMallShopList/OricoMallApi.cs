using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace OricoMallShopList
{
    /// <summary>
    /// オリコモールへアクセスし情報を取得するクラス
    /// </summary>
    class OricoMallApi
    {
        private const string OricoMallTopUrl = "http://www.oricomall.com";
        private const string OricoMallShopList = "http://www.oricomall.com/shop_list/indexed/";

        private WebBrowser Browser { get; set; }
        private string UserName { get; set; }
        private string Password { get; set; }
        private List<Shop> ShopInfoLinks { get; set; }
        private List<Shop> ShopLinks { get; set; }
        private int ShopInfoIndex { get; set; }

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
        }

        public void Start(WebBrowser browser, string userName, string password)
        {
            this.Browser = browser;
            this.UserName = userName;
            this.Password = password;

            this.ProgressMaxChanged(this, 0);
            this.ProgressValueChanged(this, 0);

            this.Move(OricoMallTopUrl, this.MoveLoginPage);
        }

        private void MoveLoginPage(object sender, NavigationEventArgs e)
        {
            // イベントハンドラを削除
            this.EndMove(MoveLoginPage);

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
                        Browser.LoadCompleted += this.Login;
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
                    this.Move(OricoMallShopList, this.GetShopList);
                }
            }
        }

        private void GetShopList(object sender, NavigationEventArgs e)
        {
            this.EndMove(this.GetShopList);

            var shopList = this.Document.getElementById("shop_list");

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
            }

            ++this.ShopInfoIndex;

            this.Move(this.CurrentShop.OricoMallUrl, this.GetShopInfo);
        }

        private void GetShopInfo(object sender, NavigationEventArgs e)
        {
            this.EndMove(this.GetShopInfo);

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
                    
                    this.Move(url, this.GetShopUrl);
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

        private void GetShopUrl(object sender, NavigationEventArgs e)
        {
            this.EndMove(this.GetShopUrl);

            var url = this.Browser.Source.AbsoluteUri;
            this.CurrentShop.Url = this.SanitizeShopUrl(url);
            this.CurrentShop.HostName = this.GetHostName(url);

            this.ShopLinks.Add(this.CurrentShop);
            this.LinkLengthChanged(this, this.ShopLinks.Count);
            
            this.GetShopInfoNext();
        }

        private void Login(object sender, NavigationEventArgs e)
        {
            var browser = (WebBrowser)sender;
            var document = (mshtml.HTMLDocument)browser.Document;

            this.EndMove(this.Login);

            var loginId = document.getElementById("loginId");
            var password = document.getElementById("password");
            var captcha = document.getElementById("captchaString");

            if (loginId != null && password != null && captcha != null)
            {
                loginId.setAttribute("value", this.UserName);
                password.setAttribute("value", this.Password);

                // 日本語の画像認証なため、日本語入力を ON にする
                captcha.style.setAttribute("ime-mode", "active");
                captcha.focus();

                var loginButton = document.getElementById("connectLogin");

                captcha.attachEvent("onkeypress", (args) =>
                {
                    loginButton.click();
                });

                // ログイン後の処理
                browser.LoadCompleted += this.LoggedIn;
            }

            else
            {
                this.Failure(this, "ログインに失敗しました。");
            }
        }

        private void LoggedIn(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            this.EndMove(this.LoggedIn);

            var loginButtton = this.Document.getElementsByClassName("btn-em-01");

            // ログインが完了していない場合
            if (loginButtton.Count > 0)
            {
                this.Login(sender, e);
                return;
            }

            this.Move(OricoMallShopList, this.GetShopList);
        }

        private void End()
        {
            this.Ended(this, this.ShopLinks);
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

        private void Move(string url, System.Windows.Navigation.LoadCompletedEventHandler handler)
        {
            this.Browser.LoadCompleted += handler;
            this.Browser.Navigate(url);
        }

        private void EndMove(System.Windows.Navigation.LoadCompletedEventHandler handler)
        {
            this.Browser.LoadCompleted -= handler;
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
    }
}
