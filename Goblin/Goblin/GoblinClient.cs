using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Goblin
{
    public class GoblinClient
    {
        GoblinWebClient gwc;
        public string character { get; private set; }
        public string faction { get; private set; }
        public Int64 money { get; private set; }
        public string avatar { get; private set; }
        public int state { get; private set; }
        public string xstoken { get; private set; }
        public GoblinConfiguration configuration { get; private set; }
        public GoblinClient()
        {
            gwc = new GoblinWebClient();
            state = 0;
        }

        public void LoadConfiguration()
        {
            StreamReader sr = File.OpenText("Golbin.json");
            string data = sr.ReadToEnd();
            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(data));
            DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(GoblinConfiguration));
            configuration = (GoblinConfiguration)jsonSerializer.ReadObject(ms);
        }

        public bool Sync()
        {
            bool ret = SyncLogin();
            ret = SyncMoney();
            
            return true;
        }

        public bool SyncLogin()
        {
            string ret = gwc.DownloadString("https://www.battlenet.com.cn/wow/zh/vault/character/auction/");
            Uri realuri = gwc.ResponseUri;

            string faction_t = realuri.Segments[6];
            faction_t = faction_t.Replace("/", "");
            faction = faction_t;

            Regex rx = new Regex(@"<img\ssrc=""([^""]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Match match = rx.Match(ret);
            if (!match.Success) return false;
            avatar = match.Groups[1].Value;
            xstoken = gwc.GetXstoken();
            return true;
        }

        public bool SyncMoney()
        {
            byte[] data = gwc.UploadData("https://www.battlenet.com.cn/wow/zh/vault/character/auction/" + faction + "/money", new byte[0]);
            string xxx = Encoding.UTF8.GetString(data);

            MemoryStream ms = new MemoryStream(data);
            DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(GoblinRequestMoney));
            GoblinRequestMoney req_money = (GoblinRequestMoney)jsonSerializer.ReadObject(ms);
            character = req_money.character.name;
            money = req_money.money;


            return true;
        }

        private GoblinRequestBid AuctionRequestBid(GoblinAuction auction, Int64 price)
        {
            NameValueCollection nvc = new NameValueCollection();
            nvc.Add("auc", auction.id);
            nvc.Add("money", price.ToString());
            nvc.Add("xtoken", xstoken);

            byte[] data = gwc.UploadValues("https://www.battlenet.com.cn/wow/zh/vault/character/auction/alliance/bid", nvc);
            string xxx = Encoding.UTF8.GetString(data);

            MemoryStream ms = new MemoryStream(data);
            DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(GoblinRequestBid));
            GoblinRequestBid req_bid = (GoblinRequestBid)jsonSerializer.ReadObject(ms);

            return req_bid;
        }

        public bool AuctionBuyout(GoblinAuction auction)
        {
            GoblinRequestBid bid = AuctionRequestBid(auction, auction.price_buyout);

            if (bid.item == null)
            {
                return false;
            }
            
            if (bid.item.owner)
            {
                return true;
            }
            return false;
        }

        public List<GoblinAuction> AuctionList(string itemid)
        {
            List<GoblinAuction> auctions = new List<GoblinAuction>();
            try
            {
                string ss = "";
                try
                {
                    ss = gwc.DownloadString("https://www.battlenet.com.cn/wow/zh/vault/character/auction/" + faction + "/browse?reverse=false&sort=unitBuyout&itemId=" + itemid);
                }
                catch
                {
                    return auctions;
                }
                Regex rx = new Regex(@"id=""auction-([0-9]+)"" class=""row", RegexOptions.Compiled | RegexOptions.IgnoreCase);

                int[] index = new int[24];
                index[0] = 0;
                int count = 0;
                while (true)
                {
                    Match match = rx.Match(ss, index[count]);
                    if (!match.Success) break;
                    string id = match.Groups[1].Value;
                    index[count] = match.Index;
                    count++;
                    index[count] = match.Index + 1;
                }

                Regex rxx = new Regex(@"<td class=""quantity"">([0-9]+)</td>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                Regex rxxx = new Regex(@"Auction.openBuyout\([0-9]+,\s([0-9]+)\);", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                Regex rx_cancel = new Regex(@"Auction.openCancel\([0-9]+\);", RegexOptions.Compiled | RegexOptions.IgnoreCase);


                for (int i = 0; i < count; i++)
                {
                    int length = ss.Length - index[i];
                    if (count - i > 2)
                    {
                        length = index[i + 1] - index[i];
                    }
                    Match match = rx.Match(ss, index[i], length);
                    string id = match.Groups[1].Value;

                    match = rx_cancel.Match(ss, index[i], length);
                    if (match.Success) continue;

                    match = rxx.Match(ss, index[i], length);
                    string quantity = match.Groups[1].Value;

                    match = rxxx.Match(ss, index[i], length);
                    string buyout = match.Groups[1].Value;

                    int q = Int32.Parse(quantity);
                    int b = Int32.Parse(buyout);

                    GoblinAuction ga = new GoblinAuction();
                    ga.id = id;
                    ga.itemid = itemid;
                    ga.price_buyout = b;
                    ga.quantity = q;

                    auctions.Add(ga);
                    //Console.WriteLine(ga.ToString());
                }
            }
            catch { }
            return auctions;
        }

        //

        public bool Login()
        {
            byte[] resp = gwc.DownloadData("https://www.battlenet.com.cn/login/zh/login.frag");
            string data = Encoding.UTF8.GetString(resp);

            Regex rx = new Regex("csrftoken\" value=\"([0-9a-f\\-]{36})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Match match = rx.Match(data);
            string csrftoken = match.Groups[1].Value;



            NameValueCollection nvc = new NameValueCollection();
            nvc.Add("accountName", configuration.username);
            nvc.Add("password", configuration.password);
            nvc.Add("persistLogin", "on");
            nvc.Add("app", "com-sc2");
            nvc.Add("csrftoken", csrftoken);

            resp = gwc.UploadValues("https://www.battlenet.com.cn/login/zh/login.frag", nvc);
            data = Encoding.UTF8.GetString(resp);
            data = data.Replace("\\", "");
            data = data.Replace("\"", "");
            rx = new Regex(@"loginTicket:(CN-[0-9a-f]{32}),", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            match = rx.Match(data);
            string id = match.Groups[1].Value;

            data = gwc.DownloadString("http://www.battlenet.com.cn/zh/?ST=" + id);

            string xstoken = gwc.GetXstoken();
            state = 1;
            
            return true;
        }
    }
    [DataContract]
    public class GoblinConfiguration
    {
        [DataMember]
        public string username { get; set; }
        [DataMember]
        public string password { get; set; }
        [DataMember]
        public GoblinItem[] items { get; set; }
        [DataMember]
        public GoblinItem[] items_extra { get; set; }
    }
    [DataContract]
    public class GoblinItem
    {
        [DataMember]
        public string id { get; set; }
        [DataMember]
        public string name { get; set; }
        [DataMember]
        public int price_buy { get; set; }
        [DataMember]
        public int price_notify { get; set; }
        public DateTime datetime_check = new DateTime(2000, 1, 1);
    }
    [DataContract]
    public class GoblinRequestCharacter
    {
        [DataMember]
        public string name { get; set; }
        [DataMember]
        public Int32 factionId { get; set; }
    }

    [DataContract]
    public class GoblinRequestItem
    {
        [DataMember]
        public string name { get; set; }
        [DataMember]
        public Int64 auctionId { get; set; }
        [DataMember]
        public bool highBidder { get; set; }
        [DataMember]
        public bool owner { get; set; }
    }

    [DataContract]
    public class GoblinRequestMoney
    {
        [DataMember]
        public GoblinRequestCharacter character { get; set; }
        [DataMember]
        public Int64 money { get; set; }
        [DataMember]
        public Int32 auctionFaction { get; set; }
    }

    [DataContract]
    public class GoblinRequestBid
    {
        [DataMember]
        public GoblinRequestCharacter character { get; set; }
        [DataMember]
        public GoblinRequestItem item { get; set; }
        [DataMember]
        public Int32 auctionFaction { get; set; }
    }
    public class GoblinAuction
    {
        public string id { get; set; }
        public string itemid { get; set; }
        public int quantity { get; set; }
        public int price_buyout { get; set; }
        public int price_unit_buyout
        {
            get { return price_buyout / quantity; }
        }


        public static string GetPriceString(int price)
        {
            int price_g = price / 10000;
            int price_s = (price % 10000) / 100;
            int price_c = price % 100;

            return price_g.ToString("00") + "," + price_s.ToString("00") + "," + price_c.ToString("00");
        }

        public override string ToString()
        {
            return String.Format("{0} {1,4} {2,12} {3,12}", id, quantity, GetPriceString(price_unit_buyout), GetPriceString(price_buyout));
        }
    }

    public class GoblinWebClient : WebClient
    {
        private CookieContainer container = new CookieContainer();
        public Uri ResponseUri { get; private set; }
        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            if (request is HttpWebRequest)
            {
                (request as HttpWebRequest).CookieContainer = container;
                (request as HttpWebRequest).UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/34.0.1847.131 Safari/537.36";
            }
            this.Encoding = System.Text.Encoding.UTF8;
            return request;
        }
        protected override WebResponse GetWebResponse(WebRequest request)
        {
            WebResponse response = base.GetWebResponse(request);
            ResponseUri = response.ResponseUri;
            return response;
        }
        public string GetXstoken()
        {
            CookieCollection cc = container.GetCookies(new Uri("http://www.battlenet.com.cn"));
            foreach (Cookie c in cc)
            {
                if (c.Name == "xstoken")
                {
                    return c.Value;
                }
            }
            return "";
        }
    }
}
