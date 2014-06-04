using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Goblin
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private GoblinClient client;
        private DispatcherTimer timer = new DispatcherTimer();
        private WindowNotify notifier = new WindowNotify();
        private int timer_count = 0;
        private int timer_state = 0;
        private int state = 0;
        private List<string> ignores;
        private Queue<GoblinItem> items;
        private Queue<GoblinItem> items_extra;
        public ObservableCollection<GoblinItem> Items { get; set; }
        private ObservableCollection<GoblinAuction> auctions;
        public ObservableCollection<GoblinAuction> Auctions
        {
            get { return auctions; }
            set
            {
                auctions = value;
                NotifyPropertyChanged("Auctions");
            }
        }
        public MainWindow()
        {
            InitializeComponent();
            client = new GoblinClient();
            Items = new ObservableCollection<GoblinItem>();
            items = new Queue<GoblinItem>();
            items_extra = new Queue<GoblinItem>();
            Auctions = new ObservableCollection<GoblinAuction>();
            ignores = new List<string>();
            LoadConfiguration();
            UI_CheckAvailable();

            timer.Tick += new EventHandler(TimerTick);
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Start();
        }

        private bool IsItemInCache(GoblinItem item)
        {
            return (DateTime.Now - item.datetime_check).TotalSeconds < 3600;
        }

        void TimerTick(object sender, EventArgs e)
        {
            if (state == 0) return;

            GoblinItem item = null;

            GoblinItem item_1 = items.First();
            GoblinItem item_2 = items_extra.First();

            if (IsItemInCache(item_1) && IsItemInCache(item_2))
            {
                if (timer_state == 0)
                {
                    item = item_1;
                }
                else
                {
                    item = item_2;
                }
            }
            else
            {
                item = item_1;
                timer_state = 0;
                if (IsItemInCache(item_1))
                {
                    item = item_2;
                    timer_state = 1;
                }
            }
            
            if (IsItemInCache(item))
            {
                timer_count++;
                LabelCount.Content = timer_count.ToString();
                if (timer_count<5) return;
            }
            timer_count = 0;

            LabelItem.Content = item.name;
            List<GoblinAuction> auctions_list = client.AuctionList(item.id);
            Auctions = new ObservableCollection<GoblinAuction>(auctions_list);

            foreach (GoblinAuction auction in auctions_list)
            {
                if (ignores.Contains(auction.id)) continue;

                if (auction.price_unit_buyout <= item.price_buy)
                {
                    if (client.AuctionBuyout(auction))
                    {
                        UI_Notify(item.name + "购买成功", "");
                    }
                    else
                    {
                        UI_Notify(item.name + "购买失败", "");
                    }
                    continue;
                }

                if (auction.price_unit_buyout <= item.price_notify)
                {
                    UI_Notify(item.name + " " + GoblinAuction.GetPriceString(auction.price_unit_buyout), "");
                }
            }
            item.datetime_check = DateTime.Now;
            if (timer_state == 0)
            {
                items.Dequeue();
                items.Enqueue(item);
                timer_state = 1;
            }
            else
            {
                items_extra.Dequeue();
                items_extra.Enqueue(item);
                timer_state = 0;
            }
        }

        private void UI_Notify(string text, string link)
        {
            notifier.Top = SystemParameters.WorkArea.BottomRight.Y - notifier.Height;
            notifier.Left = SystemParameters.WorkArea.BottomRight.X - notifier.Width;
            notifier.SetText(text);
            notifier.Show();
        }

        private void ButtonLogin_Click(object sender, RoutedEventArgs e)
        {
            ButtonLogin.IsEnabled = false;
            bool ret = client.Login();
            client.Sync();
            //client.SyncLogin();
            LabelCharacterName.Content = client.character;
            ImageAvatar.Source = new BitmapImage(new Uri(client.avatar));
            ButtonLogin.IsEnabled = true;
            if (client.faction == "alliance")
            {
                BorderAvatar.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5fb8eb"));
            }
            else if (client.faction == "horde")
            {
                BorderAvatar.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#eb1212"));
            }
            else
            {
                BorderAvatar.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#cccccc"));
            }
            ButtonLogin.Visibility= System.Windows.Visibility.Hidden;
            UI_CheckAvailable();
        }

        private void ButtonDebug_Click(object sender, RoutedEventArgs e)
        {
            state = 1;
            UI_CheckAvailable();

        }

        private void LoadConfiguration()
        {
            client.LoadConfiguration();
            Items = new ObservableCollection<GoblinItem>();

            foreach (GoblinItem gi in client.configuration.items)
            {
                Items.Add(gi);
                items.Enqueue(gi);
            }

            foreach (GoblinItem gi in client.configuration.items_extra)
            {
                Items.Add(gi);
                items_extra.Enqueue(gi);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        private void ButtonSearch_Click(object sender, RoutedEventArgs e)
        {
            List<GoblinAuction> auctions_list = client.AuctionList(ComboBoxAuctionItem.SelectedValue.ToString());
            Auctions = new ObservableCollection<GoblinAuction>(auctions_list);
        }

        private void ComboBoxAuctionItem_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UI_CheckAvailable();
        }


        private void UI_CheckAvailable()
        {
            UI_CheckSearchAvailable();
            UI_CheckLoopAvailable();
            UI_CheckReloadAvailable();
        }
        private void UI_CheckSearchAvailable()
        {
            ButtonSearch.IsEnabled = false;
            if (ComboBoxAuctionItem.SelectedValue == null) return;
            if (client.state != 1) return;
            if (state == 1) return;
            ButtonSearch.IsEnabled = true;
        }

        private void UI_CheckLoopAvailable()
        {
            ButtonLoop.IsEnabled = false;
            if (client.state != 1) return;
            ButtonLoop.IsEnabled = true;
        }

        private void UI_CheckReloadAvailable()
        {
            ButtonReload.IsEnabled = false;
            if (state == 1) return;
            ButtonReload.IsEnabled = true;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ButtonReload_Click(object sender, RoutedEventArgs e)
        {
            LoadConfiguration();
        }

        private void ButtonLoop_Click(object sender, RoutedEventArgs e)
        {
            if (state == 1)
            {
                state = 0;
                ButtonLoop.Content = "监控市场";
            }
            else
            {
                state = 1;
                ButtonLoop.Content = "暂停监控";
            }
            UI_CheckAvailable();
        }

        private void MenuItemIngore_Click(object sender, RoutedEventArgs e)
        {
            GoblinAuction ga = (GoblinAuction)ListViewAuctions.SelectedItem;
            ignores.Add(ga.id);
        }

        private void MenuItemBuyout_Click(object sender, RoutedEventArgs e)
        {
            GoblinAuction ga = (GoblinAuction)ListViewAuctions.SelectedItem;
            bool ret = client.AuctionBuyout(ga);
            if (!ret)
            {
                MessageBox.Show("购买失败");
            }

            List<GoblinAuction> auctions_list = client.AuctionList(ComboBoxAuctionItem.SelectedValue.ToString());
            Auctions = new ObservableCollection<GoblinAuction>(auctions_list);
        }

    }
}

