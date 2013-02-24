using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.IO;

using HtmlAgilityPack;
using System.Threading;
using System.Collections;
using System.Runtime.InteropServices;
using System.Linq.Expressions;
using System.Reflection;

namespace FiverrBot
{
    public partial class Form1 : Form
    {
        private Random random = new Random();

        private List<string> usernames, emails, proxies, unusedemails;
        private List<Hashtable> accountsToCreate, systemAccounts;
        private List<string> fnames, lnames, genusernames;

        int createdAccounts = 0;
        int numThreads = 3;
        int threadsRunning = 0;
        int creatorThreadsRunning = 0;

        //thread stuff
        private static List<Hashtable>[] inputArray;   //username, password, email
        private static List<Hashtable>[] resultArray;  //successful/failed, error message
        private static ManualResetEvent[] resetEvents;

        public Form1()
        {
            InitializeComponent();

            //load first names and last names into memory
            fnames = parser("firstnames.txt");
            lnames = parser("lastnames.txt");

            //Load system data and display it
            proxies = parser("systemproxies.txt");
            lblProxiesLoaded.Text = Convert.ToString(proxies.Count);

            //Initialize textbox watermarks
            TextBoxWatermarkExtensionMethod.SetWatermark(txtAddUsername, "Fiverr Username");
            TextBoxWatermarkExtensionMethod.SetWatermark(txtAddPassword, "Fiverr Password");
            TextBoxWatermarkExtensionMethod.SetWatermark(txtAddProxy, "Proxy (Optional)");
            TextBoxWatermarkExtensionMethod.SetWatermark(txtAddPort, "Port (Optional)");
            TextBoxWatermarkExtensionMethod.SetWatermark(txtAddProxyUsername, "Proxy Username (Optional)");
            TextBoxWatermarkExtensionMethod.SetWatermark(txtAddProxyPassword, "Proxy Password (Optional)");
            TextBoxWatermarkExtensionMethod.SetWatermark(txtProxyReuses, "Proxy Reuses");
            
        }

        //method used to convert files to lists
        public List<string> parser(string file)
        {
            int counter = 0;
            List<string> parsedfile = new List<string>();
            string line;

            try
            {
                using (TextReader tr = new StreamReader(file))
                {
                    while ((line = tr.ReadLine()) != null)
                    {
                        parsedfile.Add(line);
                        counter++;
                    }
                }
            }
            catch (FileNotFoundException filex)
            {
                txtLog.AppendText("Could not find " + file + ". If this is a system file it will automatically be generated for you when you import the appropriate data." + System.Environment.NewLine + txtLog.Text);
            }
            catch (Exception ex)
            {
                txtLog.AppendText("Error loading " + file + " - " + ex.Message + System.Environment.NewLine + txtLog);
            }
            
            return parsedfile;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //disable button until we're done
            //button1.Enabled = false;

            //init thread stuff
            inputArray = new List<Hashtable>[numThreads];
            resultArray = new List<Hashtable>[numThreads];
            resetEvents = new ManualResetEvent[numThreads];

            //grab the list of accounts to make. List is in format username:password:email:proxy. Store each in a hashtable. Toss it on the list.


            //feed initial input values from list, fill resetEvents. Each thread will get a new input after every iteration of createAccount().
            for (int s = 0; s < numThreads; s++)
            {
                //inputArray[s] = rand.Next(1, 5000000);
                resetEvents[s] = new ManualResetEvent(false);
                //ThreadPool.QueueUserWorkItem(new WaitCallback(DoWork), (object)s);
            }
        }

        public void createAccount(object o)
        {
            int index = (int)o; //the current thread's index
            threadsRunning++;
            creatorThreadsRunning++;
            lblThreadsRunning.SetPropertyThreadSafe(() => lblThreadsRunning.Text, Convert.ToString(threadsRunning));
            lblCreatorThreadsRunning.SetPropertyThreadSafe(() => lblCreatorThreadsRunning.Text, Convert.ToString(creatorThreadsRunning));

            foreach (Hashtable account in inputArray[index])
            {
                string username = Convert.ToString(account["username"]);
                string password = Convert.ToString(account["password"]);
                string email = "";
                string proxy = Convert.ToString(account["proxy"]);

                //lock email list and take the first one and remove it
                lock (emails)
                {
                    if (emails.Count != 0)
                    {
                        email = emails.First();
                        emails.Remove(email);
                    }
                    else
                    {
                        threadsRunning--;
                        creatorThreadsRunning--;
                        lblThreadsRunning.SetPropertyThreadSafe(() => lblThreadsRunning.Text, Convert.ToString(threadsRunning));
                        lblCreatorThreadsRunning.SetPropertyThreadSafe(() => lblCreatorThreadsRunning.Text, Convert.ToString(creatorThreadsRunning));
                        resetEvents[index].Set(); //signal the thread is done working
                    }
                }

                if (resetEvents[index].WaitOne(0))
                    return;

                //Convert proxy to a WebProxy
                WebProxy proxyObject = new WebProxy(proxy);

                //We're going to store our cookies here for now. 
                //Upon successful creation, we will store them in 
                //a file to prevent relogging, unnatural looking useage.
                CookieContainer tempCookies = new CookieContainer();

                //This will store the webserver's response to our request
                HttpWebResponse response;

                //This will store our post request data
                HttpWebRequest request;

                //This will be used to read the response
                StreamReader responseReader;

                //This will store the page in plain text
                //string thePage;

                //This will store the page for HTMLAgilityPack
                HtmlAgilityPack.HtmlDocument agilityPage;

                //These are used to properly encode our post data for the server
                UTF8Encoding encoding;
                Byte[] byteData;

                //This variable will store our unencoded post data
                string postData;

                //This is used to create our POST body
                Stream postRequestStream;

                try
                {
                    //This will help us avoid some 417 errors
                    System.Net.ServicePointManager.Expect100Continue = false;

                    /* STEP 1: Go to to join page */

                    request = (HttpWebRequest)HttpWebRequest.Create("http://fiverr.com/join");

                    request.Method = "GET";
                    request.Host = "fiverr.com";
                    request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:19.0) Gecko/20100101 Firefox/19.0";
                    request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
                    request.Headers.Add("Accept-Language: en-US,en;q=0.5");
                    request.Headers.Add("Accept-Encoding: gzip, deflate");
                    request.KeepAlive = true;
                    request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate; //used to view the page in plain text
                    request.Proxy = proxyObject;

                    //Grab the response and store the cookies
                    response = (HttpWebResponse)request.GetResponse();
                    tempCookies.Add(response.Cookies);

                    //Load the page and grab the X-CSRF Token
                    responseReader = new StreamReader(response.GetResponseStream());

                    //Create html doc
                    agilityPage = new HtmlAgilityPack.HtmlDocument();
                    agilityPage.Load(responseReader);

                    //extract token
                    string token = "";
                    string name = "";
                    foreach (HtmlNode node in agilityPage.DocumentNode.SelectNodes("//meta"))
                    {
                        name = node.GetAttributeValue("name", "");
                        if (name == "csrf-token")
                        {
                            token = node.GetAttributeValue("content", "");
                            break;
                        }
                    }

                    //extract captcha "secret" and break captcha from
                    string captchaSecret = "";
                    foreach (HtmlNode node in agilityPage.DocumentNode.SelectNodes("//input"))
                    {
                        name = node.GetAttributeValue("name", "");
                        if (name == "user[captcha_secret]")
                        {
                            captchaSecret = node.GetAttributeValue("value", "");
                            break;
                        }
                    }

                    //Break dat captcha! Note that this will only solve simple addition captchas and NOT text based ones.
                    HtmlAgilityPack.HtmlNodeCollection captchaQuestion = agilityPage.DocumentNode.SelectNodes("//div[@class='captcha-area']/span");

                    if (captchaQuestion == null)
                        throw new Exception("Unrecognized captcha type");

                    string captchaChars = captchaQuestion.ElementAt(0).InnerText;
                    captchaChars.Replace(" ", "");
                    char[] captchaCharArray = captchaChars.ToCharArray();
                    int numOne = Convert.ToInt32(captchaCharArray[0].ToString());
                    int numTwo = Convert.ToInt32(captchaCharArray[4].ToString());
                    string captchaAnswer = (numOne + numTwo).ToString();

                    //Now all we need is the "User Spam Answers" data. This is probably an encoded version of the actual answer that Fiverr uses to verify our response.
                    string userSpamAnswers = "";
                    foreach (HtmlNode node in agilityPage.DocumentNode.SelectNodes("//input"))
                    {
                        name = node.GetAttributeValue("name", "");
                        if (name == "user[spam_answers]")
                        {
                            userSpamAnswers = node.GetAttributeValue("value", "");
                            break;
                        }
                    }

                    response.Close();

                    /* STEP 2: Check user */
                    postData = "username=" + username; //change the username here to a variable

                    //Properly encode our data for the server
                    encoding = new UTF8Encoding();
                    byteData = encoding.GetBytes(postData);

                    request = (HttpWebRequest)WebRequest.Create("http://fiverr.com/checkuser");

                    request.Method = "POST";
                    request.Host = "fiverr.com";
                    request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:19.0) Gecko/20100101 Firefox/19.0";
                    request.Accept = "text/javascript";
                    request.Headers.Add("Accept-Language: en-US,en;q=0.5");
                    request.Headers.Add("Accept-Encoding: gzip, deflate");
                    request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                    request.Headers.Add("X-Requested-With: XMLHttpRequest");
                    request.Headers.Add("X-CSRF-Token: " + token);
                    request.Referer = "http://fiverr.com/join";
                    request.ContentLength = byteData.Length;
                    request.CookieContainer = tempCookies;
                    request.KeepAlive = true;
                    request.Headers.Add("Pragma: no-cache");
                    request.Headers.Add("Cache-Control: no-cache");
                    request.Proxy = proxyObject;

                    postRequestStream = request.GetRequestStream();
                    postRequestStream.Write(byteData, 0, byteData.Length);
                    postRequestStream.Close();

                    //Grab the response
                    response = (HttpWebResponse)request.GetResponse();
                    response.Close();

                    /* STEP 3: Check suspicious email */
                    //Properly encode our data for the server
                    encoding = new UTF8Encoding();
                    byteData = encoding.GetBytes(postData);

                    request = (HttpWebRequest)WebRequest.Create("http://fiverr.com/check_suspicious_email");

                    request.Method = "POST";
                    request.Host = "fiverr.com";
                    request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:19.0) Gecko/20100101 Firefox/19.0";
                    request.Accept = "text/javascript";
                    request.Headers.Add("Accept-Language: en-US,en;q=0.5");
                    request.Headers.Add("Accept-Encoding: gzip, deflate");
                    request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                    request.Headers.Add("X-Requested-With: XMLHttpRequest");
                    request.Headers.Add("X-CSRF-Token: " + token);  //we need to find this token in the previous response
                    request.Referer = "http://fiverr.com/join";
                    request.ContentLength = byteData.Length;
                    request.CookieContainer = tempCookies;
                    request.KeepAlive = true;
                    request.Headers.Add("Pragma: no-cache");
                    request.Headers.Add("Cache-Control: no-cache");
                    request.Proxy = proxyObject;

                    postRequestStream = request.GetRequestStream();
                    postRequestStream.Write(byteData, 0, byteData.Length);
                    postRequestStream.Close();

                    //Grab the response
                    response = (HttpWebResponse)request.GetResponse();
                    response.Close();

                    /* STEP 4: Register the user */
                    //we need to replace values with variables here
                    postData = "utf8=%E2%9C%93" +
                               "&authenticity_token=" + token +
                               "&user%5Binvitation_token%5D=&user%5Bemail%5D=" + email +
                               "&user%5Busername%5D=" + username +
                               "&user%5Bpassword%5D=" + password +
                               "&user%5Bcaptcha_solution%5D=" + captchaAnswer +
                               "&user%5Bcaptcha_secret%5D=" + captchaSecret +
                               "&user%5Bspam_answers%5D=" + userSpamAnswers +
                               "&user%5Bspam_answer%5D=" +
                               "&user%5Bterms_of_use%5D=0" +
                               "&user%5Bterms_of_use%5D=1";

                    //Properly encode our data for the server
                    encoding = new UTF8Encoding();
                    byteData = encoding.GetBytes(postData);

                    request = (HttpWebRequest)WebRequest.Create("http://fiverr.com/users");

                    request.Method = "POST";
                    request.Host = "fiverr.com";
                    request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:19.0) Gecko/20100101 Firefox/19.0";
                    request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
                    request.Headers.Add("Accept-Language: en-US,en;q=0.5");
                    request.Headers.Add("Accept-Encoding: gzip, deflate");
                    request.Referer = "http://fiverr.com/join";
                    request.CookieContainer = tempCookies;
                    request.KeepAlive = true;
                    request.ContentType = "application/x-www-form-urlencoded";
                    request.ContentLength = byteData.Length;
                    request.Proxy = proxyObject;
                    request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate; //used to view the page in plain text

                    postRequestStream = request.GetRequestStream();
                    postRequestStream.Write(byteData, 0, byteData.Length);
                    postRequestStream.Close();

                    //Grab the response and store the cookies
                    response = (HttpWebResponse)request.GetResponse();
                    tempCookies.Add(response.Cookies);

                    //grab the page
                    responseReader = new StreamReader(response.GetResponseStream());
                    //thePage = responseReader.ReadToEnd();

                    //We can use HTMLAgilityPack to scrape the redirect link here
                    //Create html doc
                    agilityPage = new HtmlAgilityPack.HtmlDocument();
                    agilityPage.Load(responseReader);

                    //search html for "please verify your account"
                    if (agilityPage.DocumentNode.InnerHtml.Contains("Please activate your account"))
                    {
                        //SUCCESS!
                        //store the cookies in a file (we'll do this later when we actually need it)

                        //increase successful counter
                        createdAccounts++;
                        lblAccountsCreated.SetPropertyThreadSafe(() => lblAccountsCreated.Text, Convert.ToString(createdAccounts));
                        txtLog.SetPropertyThreadSafe(() => txtLog.Text, "Successfully created account " + username + System.Environment.NewLine + txtLog.Text);
                    }
                }
                catch (WebException wex)
                {
                    lock(unusedemails)
                        emails.Add(email); //readd our unused email

                    txtLog.SetPropertyThreadSafe(() => txtLog.Text,  "Error creating account " + username + " - " + wex.Message + System.Environment.NewLine + txtLog.Text );
                }
                catch (Exception ex)
                {
                    lock (unusedemails)
                        emails.Add(email); //readd our unused email

                    txtLog.SetPropertyThreadSafe(() => txtLog.Text, "Error creating account " + username + " - " + ex.Message + System.Environment.NewLine + txtLog.Text);
                }
            }//foreach account
            threadsRunning--;
            creatorThreadsRunning--;
            lblThreadsRunning.SetPropertyThreadSafe(() => lblThreadsRunning.Text, Convert.ToString(threadsRunning));
            lblCreatorThreadsRunning.SetPropertyThreadSafe(() => lblCreatorThreadsRunning.Text, Convert.ToString(creatorThreadsRunning));
            resetEvents[index].Set(); //signal the thread is done working
        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void groupBox3_Enter(object sender, EventArgs e)
        {

        }

        //loads and saves proxies
        private void btnLoadProxies_Click(object sender, EventArgs e)
        {
            DialogResult result = openFileDialog1.ShowDialog(); // Show the dialog.
            if (result == DialogResult.OK) // Test result.
            {
                string file = openFileDialog1.FileName;
                try
                {
                    proxies = parser(file);
                    lblProxiesLoaded.Text = Convert.ToString(proxies.Count);

                    //save proxies to system
                    using (System.IO.StreamWriter proxiesfile = new System.IO.StreamWriter(@"systemproxies.txt"))
                    {
                        foreach (string line in proxies)
                        {
                            proxiesfile.WriteLine(line);
                        }
                    }

                    //Notify log that we laoded new proxies
                    //txtLog.AppendText("New proxy set successfully loaded!" + System.Environment.NewLine);
                }
                catch (Exception ex)
                {
                    //Notify log that we failed to load proxies
                    //txtLog.AppendText("Failed to load new proxy set - " + ex.Message + '\n');
                }
            }
        }

        //creates account list and delegates the list to the threads
        private void btnCreateAccounts_Click(object sender, EventArgs e)
        {
            try
            {
                //init unusedemails
                unusedemails = new List<string>();

                //parse usernames and emails
                usernames = new List<string>();
                foreach (string username in txtUsernames.Lines)
                {   
                    if (username != "")
                        usernames.Add(username);
                }

                emails = new List<string>();
                foreach (string email in txtEmails.Lines)
                    emails.Add(email);

                /* No longer need to have more emails than usernames because we pull from the list at creation time and reuse emails now */
                //verify that we have equal or more emails than usernames
                //if (emails.Count >= usernames.Count)
                //{
                    //disable start button, enable stop button
                    btnCreateAccounts.Enabled = false;
                    btnStopCreating.Enabled = true;

                    //create list of accounts in hashtable format
                    accountsToCreate = new List<Hashtable>();
                    foreach (string username in usernames)
                    {
                        /* Emails are now pulled from the list at creation time so we can rotate properly. */
                        /* Proxies are split from the username and added to the hashtable that way. */
                        Hashtable newAccount = new Hashtable();

                        //split username/proxy
                        string[] userprox = username.Split(':');
                        newAccount["username"] = userprox[0];

                        if (userprox[1] != null)
                            newAccount["proxy"] = userprox[1];
                        else
                            newAccount["proxy"] = "127.0.0.1:8080"; //no proxy

                        newAccount["password"] = RandomPassword.Generate();
                        accountsToCreate.Add(newAccount);
                    }

                    //delegate the accounts to the threads
                    List<List<Hashtable>> splitAccounts = Split(accountsToCreate, accountsToCreate.Count / numThreads);

                    //if there are less account lists than threads, change the number of threads to run
                    if (splitAccounts.Count > numThreads)
                        numThreads = splitAccounts.Count;

                    //init thread stuff
                    inputArray = new List<Hashtable>[numThreads];
                    resultArray = new List<Hashtable>[numThreads];
                    resetEvents = new ManualResetEvent[numThreads];

                    for (int i = 0; i <= numThreads-1; ++i)
                    {
                        inputArray[i] = splitAccounts[i];
                        resetEvents[i] = new ManualResetEvent(false);
                        ThreadPool.QueueUserWorkItem(new WaitCallback(createAccount), (object)i);
                    }

                    //WaitHandle.WaitAll(resetEvents);
                    //MessageBox.Show("Account creator has finished! W00t!.", "Done!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    btnCreateAccounts.Enabled = true;
                    btnStopCreating.Enabled = false;
                //}
                //else
                //{
                    //tell the user they need more emails than accounts
                   // MessageBox.Show("Not enough emails. Please supply more or equal emails to usernames.", "Need more emails than usernames!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                //}
                
            }
            catch (Exception ex)
            {
                //txtLog.SetPropertyThreadSafe(() => txtLog.Text, txtLog.Text + "Error initializing threads. Make sure you have some usernames/emails in the list. " + ex.Message + System.Environment.NewLine);
            }
            

            //update proxy count and save them
            lblProxiesLoaded.SetPropertyThreadSafe(() => lblProxiesLoaded.Text, Convert.ToString(proxies.Count));

            //save proxies to system
            using (System.IO.StreamWriter proxiesfile = new System.IO.StreamWriter(@"systemproxies.txt"))
            {
                foreach (string line in proxies)
                {
                    proxiesfile.WriteLine(line);
                }
            }

            //Notify log that we laoded new proxies
            //txtLog.AppendText("Used proxies have been removed from the system list." + System.Environment.NewLine);
        }
        
        //split a list into a list of lists in order to split accounts between threads
        public static List<List<Hashtable>> Split(List<Hashtable> source, int threads)
        {
            return source
                .Select((x, i) => new { Index = i, Value = x })
                .GroupBy(x => x.Index / threads)
                .Select(x => x.Select(v => v.Value).ToList())
                .ToList();
        }

        private void btnGenerateUsernames_Click(object sender, EventArgs e)
        {
            int proxyreuses = 0;
            try
            {
                genusernames = new List<string>();
                txtGeneratedUsernames.Clear();

                //determine proxy reuses
                if (txtProxyReuses.Text != null)
                    proxyreuses = Convert.ToInt32(txtProxyReuses.Text);
                else
                    proxyreuses = 0;

                for(int i = 0; i < (proxies.Count * (proxyreuses+1)); ++i)
                {
                    string username = fnames[random.Next(0, fnames.Count)] + lnames[random.Next(0, lnames.Count)] + random.Next(100, 99999);

                    if (username.Length > 15)
                        username = username.Substring(0, 15);

                    txtGeneratedUsernames.AppendText(username + System.Environment.NewLine);
                    genusernames.Add(username);
                }
                lblAccountsGenerated.Text = Convert.ToString(txtGeneratedUsernames.Lines.Count()-1);
            }
            catch (Exception ex)
            {

            }
        }

        private void btnTieProxies_Click(object sender, EventArgs e)
        {
            int proxyreuses = 0;

            try
            {
                //determine proxy reuses
                if (txtProxyReuses.Text != null)
                    proxyreuses = Convert.ToInt32(txtProxyReuses.Text);
                else
                    proxyreuses = 0;

                txtGeneratedUsernames.Clear();
                for(int i = 0; i < genusernames.Count; i+=(proxyreuses+1))
                {
                    for (int p = 0; p <= proxyreuses; ++p)
                    {
                        string userproxy = genusernames[i + p] + ':' + proxies[i];
                        txtGeneratedUsernames.AppendText(userproxy + System.Environment.NewLine);
                        //genusernames.Add(userproxy);
                    }
                }
                lblAccountsGenerated.Text = Convert.ToString(txtGeneratedUsernames.Lines.Count()-1);
                genusernames.Clear();
                foreach (string userprox in txtGeneratedUsernames.Lines)
                    genusernames.Add(userprox);
            }
            catch (Exception ex)
            {

            }
        }

        private void btnExportToCreator_Click(object sender, EventArgs e)
        {
            //check that creator is not running before exporting
            if (creatorThreadsRunning == 0)
            {
                txtUsernames.Text = txtGeneratedUsernames.Text;
            }
        }
    }

    //method to create text box watermarks
    public static class TextBoxWatermarkExtensionMethod
    {
        private const uint ECM_FIRST = 0x1500;
        private const uint EM_SETCUEBANNER = ECM_FIRST + 1;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, uint wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);

        public static void SetWatermark(this TextBox textBox, string watermarkText)
        {
            SendMessage(textBox.Handle, EM_SETCUEBANNER, 0, watermarkText);
        }
    }

    public static class ExtensionHelpers
    {
        //the following block of code allows us to update controls from separate threads like this:
        //myLabel.SetPropertyThreadSafe(() => myLabel.Text, status);
        private delegate void SetPropertyThreadSafeDelegate<TResult>(Control @this, Expression<Func<TResult>> property, TResult value);
        public static void SetPropertyThreadSafe<TResult>(this Control @this, Expression<Func<TResult>> property, TResult value)
        {
            var propertyInfo = (property.Body as MemberExpression).Member as PropertyInfo;

            if (propertyInfo == null ||
                !@this.GetType().IsSubclassOf(propertyInfo.ReflectedType) ||
                @this.GetType().GetProperty(propertyInfo.Name, propertyInfo.PropertyType) == null)
            {
                throw new ArgumentException("The lambda expression 'property' must reference a valid property on this Control.");
            }

            if (@this.InvokeRequired)
            {
                @this.Invoke(new SetPropertyThreadSafeDelegate<TResult>(SetPropertyThreadSafe), new object[] { @this, property, value });
            }
            else
            {
                @this.GetType().InvokeMember(propertyInfo.Name, BindingFlags.SetProperty, null, @this, new object[] { value });
            }
        }
    }
}


