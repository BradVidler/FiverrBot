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

        private List<string> usernames, emails, proxies;
        private List<Hashtable> accountsToCreate, systemAccounts;

        int createdAccounts = 0;
        int numThreads = 1;
        int threadsRunning = 0;
        int creatorThreadsRunning = 0;

        //thread stuff
        private static List<Hashtable>[] inputArray;   //username, password, email
        private static List<Hashtable>[] resultArray;  //successful/failed, error message
        private static ManualResetEvent[] resetEvents;

        public Form1()
        {
            InitializeComponent();

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
                txtLog.AppendText("Could not find " + file + ". If this is a system file it will automatically be generated for you when you import the appropriate data.");
            }
            catch (Exception ex)
            {
                txtLog.AppendText("Error loading " + file + " - " + ex.Message);
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
                string email = Convert.ToString(account["email"]);
                string proxy = Convert.ToString(account["proxy"]);

                //remove username, email and proxy from lists
                usernames.Remove(username);
                emails.Remove(email);

                if (proxy != "127.0.0.1:8080")
                    proxies.Remove(proxy);

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

                    //GET http://fiverr.com/join HTTP/1.1
                    //Host: fiverr.com
                    //User-Agent: Mozilla/5.0 (Windows NT 6.1; WOW64; rv:19.0) Gecko/20100101 Firefox/19.0
                    //Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8
                    //Accept-Language: en-US,en;q=0.5
                    //Accept-Encoding: gzip, deflate
                    //Connection: keep-alive

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

                    //Load the page and grab the X-CSRF Token from this code:
                    /*
                        <meta content="authenticity_token" name="csrf-param" />
                        <meta content="JbzGgE985PHMSpi5voFHqlV3PHAUzLzQNOqVJt3HhOQ=" name="csrf-token" />
                    */

                    responseReader = new StreamReader(response.GetResponseStream());
                    //thePage = responseReader.ReadToEnd();

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
                    /*
                    <div class="captcha">
                        <label class="style1"></label><br/>
                        <div class="captcha-area" style="display:block">
                     * 
                        <span>2 + 9 =</span> //This is the captcha we need to break. Yeah... It's pretty difficult...
                     * 
                        <input class="text style2" id="user_captcha_solution" name="user[captcha_solution]" size="30" type="text" />
                        <input id="user_captcha_secret" name="user[captcha_secret]" type="hidden" value="zleiGnFdA7nsUDMeVaTDnNlzs2qwhJMK0k4mNfUwfFc=" />
                    */

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
                    //<input id="user_spam_answers" name="user[spam_answers]" type="hidden" 
                    //       value="$2a$10$aGPCeWmh2oibmUHWdb23YOU0JfrQil1X7c9LC8KE2c6X5IeC.4yKG-$2a$10$aGPCeWmh2oibmUHWdb23YOgVvL0cyjaWyo3OlQ9d2LIT4Yh/YSf.K" />

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

                    //POST http://fiverr.com/checkuser HTTP/1.1
                    //Host: fiverr.com
                    //User-Agent: Mozilla/5.0 (Windows NT 6.1; WOW64; rv:19.0) Gecko/20100101 Firefox/19.0
                    //Accept: text/javascript
                    //Accept-Language: en-US,en;q=0.5
                    //Accept-Encoding: gzip, deflate
                    //Content-Type: application/x-www-form-urlencoded; charset=UTF-8
                    //X-Requested-With: XMLHttpRequest
                    //X-CSRF-Token: JbzGgE985PHMSpi5voFHqlV3PHAUzLzQNOqVJt3HhOQ=
                    //Referer: http://fiverr.com/join
                    //Content-Length: 24
                    //Cookie: _fiverr_session=BAh7ByIPc2Vzc2lvbl9pZEkiJWI1NDQyNmM0MzY4MjIyYzIwYTY4NmJlN2NlNzc5OWUxBjoGRVQiEF9jc3JmX3Rva2VuSSIxSmJ6R2dFOTg1UEhNU3BpNXZvRkhxbFYzUEhBVXpMelFOT3FWSnQzSGhPUT0GOwBG--68198f6975333b81cb957393176d0727cdadbded; __utma=156287140.1399514973.1361481945.1361481945.1361481945.1; __utmb=156287140.1.10.1361481945; __utmc=156287140; __utmz=156287140.1361481945.1.1.utmcsr=(direct)|utmccn=(direct)|utmcmd=(none)
                    //Connection: keep-alive
                    //Pragma: no-cache
                    //Cache-Control: no-cache

                    //username=SuperSweetCandy

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

                    //POST http://fiverr.com/check_suspicious_email HTTP/1.1
                    //Host: fiverr.com
                    //User-Agent: Mozilla/5.0 (Windows NT 6.1; WOW64; rv:19.0) Gecko/20100101 Firefox/19.0
                    //Accept: text/javascript
                    //Accept-Language: en-US,en;q=0.5
                    //Accept-Encoding: gzip, deflate
                    //Content-Type: application/x-www-form-urlencoded; charset=UTF-8
                    //X-Requested-With: XMLHttpRequest
                    //X-CSRF-Token: JbzGgE985PHMSpi5voFHqlV3PHAUzLzQNOqVJt3HhOQ=
                    //Referer: http://fiverr.com/join
                    //Content-Length: 29
                    //Cookie: _fiverr_session=BAh7ByIPc2Vzc2lvbl9pZEkiJWI1NDQyNmM0MzY4MjIyYzIwYTY4NmJlN2NlNzc5OWUxBjoGRVQiEF9jc3JmX3Rva2VuSSIxSmJ6R2dFOTg1UEhNU3BpNXZvRkhxbFYzUEhBVXpMelFOT3FWSnQzSGhPUT0GOwBG--68198f6975333b81cb957393176d0727cdadbded; __utma=156287140.1399514973.1361481945.1361481945.1361481945.1; __utmb=156287140.1.10.1361481945; __utmc=156287140; __utmz=156287140.1361481945.1.1.utmcsr=(direct)|utmccn=(direct)|utmcmd=(none)
                    //Connection: keep-alive
                    //Pragma: no-cache
                    //Cache-Control: no-cache

                    //email=MyCoolEmail@hotmail.com

                    //postData = email; //change the email here to a variable

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

                    //POST http://fiverr.com/users HTTP/1.1
                    //Host: fiverr.com
                    //User-Agent: Mozilla/5.0 (Windows NT 6.1; WOW64; rv:19.0) Gecko/20100101 Firefox/19.0
                    //Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8
                    //Accept-Language: en-US,en;q=0.5
                    //Accept-Encoding: gzip, deflate
                    //Referer: http://fiverr.com/join
                    //Cookie: _fiverr_session=BAh7ByIPc2Vzc2lvbl9pZEkiJWI1NDQyNmM0MzY4MjIyYzIwYTY4NmJlN2NlNzc5OWUxBjoGRVQiEF9jc3JmX3Rva2VuSSIxSmJ6R2dFOTg1UEhNU3BpNXZvRkhxbFYzUEhBVXpMelFOT3FWSnQzSGhPUT0GOwBG--68198f6975333b81cb957393176d0727cdadbded; __utma=156287140.1399514973.1361481945.1361481945.1361481945.1; __utmb=156287140.1.10.1361481945; __utmc=156287140; __utmz=156287140.1361481945.1.1.utmcsr=(direct)|utmccn=(direct)|utmcmd=(none)
                    //Connection: keep-alive
                    //Content-Type: application/x-www-form-urlencoded
                    //Content-Length: 560

                    //utf8=%E2%9C%93&authenticity_token=JbzGgE985PHMSpi5voFHqlV3PHAUzLzQNOqVJt3HhOQ%3D&user%5Binvitation_token%5D=&user%5Bemail%5D=MyCoolEmail%40hotmail.com&user%5Busername%5D=SuperSweetCandy&user%5Bpassword%5D=coolpasswordbro&user%5Bcaptcha_solution%5D=11&user%5Bcaptcha_secret%5D=zleiGnFdA7nsUDMeVaTDnNlzs2qwhJMK0k4mNfUwfFc%3D%0D%0A&user%5Bspam_answers%5D=%242a%2410%24aGPCeWmh2oibmUHWdb23YOU0JfrQil1X7c9LC8KE2c6X5IeC.4yKG-%242a%2410%24aGPCeWmh2oibmUHWdb23YOgVvL0cyjaWyo3OlQ9d2LIT4Yh%2FYSf.K&user%5Bspam_answer%5D=&user%5Bterms_of_use%5D=0&user%5Bterms_of_use%5D=1

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
                    }
                }
                catch (WebException wex)
                {
                    txtLog.SetPropertyThreadSafe(() => txtLog.Text, txtLog.Text + "Error creating account " + username + " - " + wex.Message + System.Environment.NewLine);
                }
                catch (Exception ex)
                {
                    txtLog.SetPropertyThreadSafe(() => txtLog.Text, txtLog.Text + "Error creating account " + username + " - " + ex.Message + System.Environment.NewLine);
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
                    txtLog.AppendText("New proxy set successfully loaded!" + System.Environment.NewLine);
                }
                catch (Exception ex)
                {
                    //Notify log that we failed to load proxies
                    txtLog.AppendText("Failed to load new proxy set - " + ex.Message + '\n');
                }
            }
        }

        //creates account list and delegates the list to the threads
        private void btnCreateAccounts_Click(object sender, EventArgs e)
        {
            try
            {
                //parse usernames and emails
                usernames = new List<string>();
                foreach (string username in txtUsernames.Lines)
                    usernames.Add(username);

                emails = new List<string>();
                foreach (string email in txtEmails.Lines)
                    emails.Add(email);

                //verify that we have equal or more emails than usernames
                if (emails.Count >= usernames.Count)
                {
                    //disable start button, enable stop button
                    btnCreateAccounts.Enabled = false;
                    btnStopCreating.Enabled = true;

                    //create list of accounts in hashtable format
                    int proxynum = 0;
                    int emailnum = 0;
                    accountsToCreate = new List<Hashtable>();
                    foreach (string username in usernames)
                    {
                        Hashtable newAccount = new Hashtable();
                        newAccount["username"] = username;
                        newAccount["email"] = emails[emailnum];

                        if (proxynum > proxies.Count)
                            proxynum = 0;   //used all proxies, reset the list

                        if (proxies.Count == 0)
                            newAccount["proxy"] = "127.0.0.1:8080"; //no proxy
                        else
                            newAccount["proxy"] = proxies[proxynum];

                        newAccount["password"] = RandomPassword.Generate();

                        emailnum++;
                        proxynum++;
                        accountsToCreate.Add(newAccount);
                    }

                    //delegate the accounts to the threads
                    List<List<Hashtable>> splitAccounts = Split(accountsToCreate, numThreads);

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
                }
                else
                {
                    //tell the user they need more emails than accounts
                    MessageBox.Show("Not enough emails. Please supply more or equal emails to usernames.", "Need more emails than usernames!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                
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


