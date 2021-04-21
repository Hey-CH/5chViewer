using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
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

namespace _5chViewerWPF {
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window {
        ViewModel vm = new ViewModel();
        public MainWindow() {
            InitializeComponent();
            this.DataContext = vm;

            string html = "";
            //まずHTMLの取得
            HttpWebRequest req = WebRequest.CreateHttp("https://www2.5ch.net/5ch.html");
            using (var res = req.GetResponse()) {
                using (var r = res.GetResponseStream()) {
                    using (var sr = new StreamReader(r, Encoding.GetEncoding(932))) {
                        html = sr.ReadToEnd();
                    }
                }
            }
            //Treeの作成
            bool stTag = false;
            bool edTag = false;
            bool isAttr = false;
            string currentTag = null;
            string currentAttr = null;
            string tmp = null;
            Regex regex = new Regex("(?:href=)(.*)",RegexOptions.IgnoreCase);
            for (int i = 0; i < html.Length; i++) {
                if (html[i] == '<') {
                    if (html[i + 1] == '/') {
                        if (tmp != null) {
                            if (currentTag == "B") {
                                TreeSource tn = new TreeSource(tmp);
                                vm.Menus.Add(tn);
                            } else if (currentTag == "A" && vm.Menus.Count > 0) {
                                string href = regex.Match(currentAttr).Groups[1].Value + "subback.html";
                                TreeSource tn = new TreeSource(tmp);
                                tn.URL = href;
                                vm.Menus[vm.Menus.Count - 1].Children.Add(tn);
                            }
                        }
                        edTag = true;
                    } else {
                        stTag = true;
                        tmp = null;
                        currentTag = null;
                        currentAttr = null;
                    }
                } else if (html[i] == '>') {
                    if (edTag) currentTag = null;
                    stTag = false;
                    edTag = false;
                    isAttr = false;
                } else if (html[i] == ' ' && stTag) {
                    isAttr = true;
                } else {
                    if (isAttr) currentAttr += html[i];
                    else if (stTag) currentTag += html[i];
                    else if (!edTag && (currentTag == "B" || currentTag == "A")) {
                        tmp += html[i];
                        if (tmp == "他のサイト") break;
                    }
                }
            }
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
            if (((TreeSource)e.NewValue).URL == null) return;
            menuURL = ((TreeSource)e.NewValue).URL;
            string html = "";
            Regex regex = new Regex("(?:<a href=\")(.*)(?:\">)(.*)(?:</a>)");
            //まずHTMLの取得
            try {
                HttpWebRequest req = WebRequest.CreateHttp(((TreeSource)e.NewValue).URL);
                using (var res = req.GetResponse()) {
                    using (var r = res.GetResponseStream()) {
                        using (var sr = new StreamReader(r, Encoding.GetEncoding(932))) {
                            html = sr.ReadToEnd();
                        }
                    }
                }
            } catch (Exception ex) {
                System.Windows.MessageBox.Show("HTMLの取得に失敗しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            //正規表現でhref属性値とスレッドタイトルを取得する
            vm.Threads.Clear();
            try {
                var st = html.LastIndexOf("<small id=\"trad\">");
                var ed = html.IndexOf("</small>", st);
                var ms = regex.Matches(html);
                foreach (Match m in ms) {
                    if (m.Index < st || m.Index > ed) continue;
                    vm.Threads.Add(new Thread(m.Groups[2].Value, m.Groups[1].Value));
                }
            } catch (Exception ex) {
                System.Windows.MessageBox.Show("HTMLの読取に失敗しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        string html = "";
        string menuURL = "";
        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (vm.Threads.Count<=0 || ((Thread)e.AddedItems[0]).URL == null) return;
            var url = menuURL;
            //https://hayabusa9.5ch.net/newsの形にする
            url = url.Substring(0, url.Length - ("/subback.html").Length);
            ////https://hayabusa9.5ch.net/test/read.cgi/news/の形にする
            url = url.Substring(0, url.LastIndexOf("/")) + "/test/read.cgi" + url.Substring(url.LastIndexOf("/")) + "/";
            var sub = ((Thread)e.AddedItems[0]).URL;
            sub = sub.Substring(0, sub.Length - ("150").Length);
            url = url + sub;
            //まずHTMLの取得
            try {
                HttpWebRequest req = WebRequest.CreateHttp(url);
                using (var res = req.GetResponse()) {
                    using (var r = res.GetResponseStream()) {
                        using (var sr = new StreamReader(r, Encoding.GetEncoding(932))) {
                            html = sr.ReadToEnd();
                        }
                    }
                }
            } catch (Exception ex) {
                System.Windows.MessageBox.Show("HTMLの取得に失敗しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            //BackgroundWorker bw = new BackgroundWorker();
            //bw.DoWork += Bw_DoWork;
            //bw.RunWorkerAsync();


            Regex numberReg = new Regex("(<span class=\"number\">)(.*?)(</span>)");
            Regex nameReg = new Regex("(<span class=\"name\">)(.*?)(</span>)");
            Regex dateReg = new Regex("(<span class=\"date\">)(.*?)(</span>)");
            Regex uidReg = new Regex("(<span class=\"uid\">)(.*?)(</span>)");
            Regex escapedReg = new Regex("(<span class=\"escaped\">)(.*?)(</span>)");
            int index = 0;
            FlowDocument doc = new FlowDocument();
            List<string> escapeds = new List<string>();
            while (true) {
                //numberの取得
                var m = numberReg.Match(html, index);
                if (!m.Success) break;
                var number = m.Groups[2].Value;
                index = m.Index;
                //nameの取得
                m = nameReg.Match(html, index);
                if (!m.Success) break;
                var name = m.Groups[2].Value;
                index = m.Index;
                //dateの取得
                m = dateReg.Match(html, index);
                if (!m.Success) break;
                var date = m.Groups[2].Value;
                index = m.Index;
                //uidの取得
                m = uidReg.Match(html, index);
                if (!m.Success) break;
                var uid = m.Groups[2].Value;
                index = m.Index;
                //escapedの取得
                m = escapedReg.Match(html, index);
                if (!m.Success) break;
                var escaped = m.Groups[2].Value;
                escaped = escaped.Replace("<br>", "\r\n");
                index = m.Index;

                //nameとescapedの調整
                name = Regex.Replace(name, "<[^>]*?>", "");
                escaped = Regex.Replace(escaped, "<[^>]*?>", "").Replace("&gt;", ">").Replace("&lt;", "<").Replace("&amp;", "&");
                escapeds.Add(escaped);//ツールチップに表示するため用


                //tmp += number + " " + name + " " + date + " " + uid + "\r\n\r\n" + escaped + "\r\n\r\n\r\n";
                var p1 = new Paragraph();
                p1.Inlines.Add(new Run(number));
                p1.Inlines.Add(new Run(" " + name));
                p1.Inlines.Add(new Run(" " + date));
                p1.Inlines.Add(new Run(" " + uid));
                p1.Inlines.Add(new LineBreak());

                Regex urlreg = new Regex(@"http(s)?://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?");
                Regex tagereg = new Regex(@">>?(\d+)");
                var p2 = new Paragraph();
                string res = "";
                for (int i = 0; i < escaped.Length; i++) {
                    string tmp = escaped.Substring(i);
                    var m1 = urlreg.Matches(tmp);
                    var m2 = tagereg.Matches(tmp);
                    if (m1.Count > 0 && m1[0].Index == 0) {
                        p2.Inlines.Add(new Run(res));
                        res = "";
                        Hyperlink link = new Hyperlink(new Run(m1[0].Value));
                        link.NavigateUri = new Uri(m1[0].Value);
                        link.MouseLeftButtonDown += (s, ev) => { System.Diagnostics.Process.Start(((Hyperlink)s).NavigateUri.AbsoluteUri); };
                        p2.Inlines.Add(link);
                        i += m1[0].Value.Length - 1;
                    } else if (m2.Count > 0 && m2[0].Index == 0) {
                        try {
                            p2.Inlines.Add(new Run(res));
                            res = "";
                            var tage = int.Parse(m2[0].Groups[1].Value);//>>99の99
                            Hyperlink link = new Hyperlink(new Run(m2[0].Value));
                            var tt = new ToolTip();
                            var tb = new TextBlock(new Run(escapeds[tage - 1]));
                            tb.TextWrapping = TextWrapping.Wrap;
                            tt.Content = tb;
                            link.ToolTip = tt;
                            link.MouseEnter += (s, ev) => { tt.IsOpen = true; };
                            link.MouseLeave += (s, ev) => { tt.IsOpen = false; };
                            p2.Inlines.Add(link);
                            i += m2[0].Value.Length - 1;
                        } catch (Exception ex) {
                        }
                    } else {
                        res += escaped[i];
                    }
                }
                if (res.Length > 0) p2.Inlines.Add(new Run(res));
                p2.Inlines.Add(new LineBreak());

                doc.Blocks.Add(p1);
                doc.Blocks.Add(p2);
            }
            //別スレッドにするとなぜか以下のようにしてもエラーになってしまう。
            //this.Dispatcher.Invoke(() => { richTextBox1.Document = doc; });
            richTextBox1.Document = doc;
        }
    }
    public class ViewModel : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        ObservableCollection<TreeSource> _Menus = new ObservableCollection<TreeSource>();
        public ObservableCollection<TreeSource> Menus {
            get { return _Menus; }
            set {
                _Menus = value;
                OnPropertyChanged("Menus");
            }
        }

        ObservableCollection<Thread> _Threads = new ObservableCollection<Thread>();
        public ObservableCollection<Thread> Threads {
            get { return _Threads; }
            set {
                _Threads = value;
                OnPropertyChanged("Threads");
            }
        }

        string _Contents = "";
        public string Contents {
            get { return _Contents; }
            set {
                _Contents = value;
                OnPropertyChanged("Contents");
            }
        }
    }
    public class TreeSource {
        public TreeSource Parent { get; set; }
        public ObservableCollection<TreeSource> Children { get; set; }
        public bool IsExpanded { get; set; }
        public string Text { get; set; }
        public string URL { get; set; }
        public TreeSource(string text) {
            Text = text;
            Children = new ObservableCollection<TreeSource>();
        }
    }
    public class Thread {
        public string Text { get; set; }
        public string URL { get; set; }
        public Thread(string text,string url) {
            Text = text;
            URL = url;
        }
    }
}
