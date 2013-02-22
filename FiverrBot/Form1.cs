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

namespace FiverrBot
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //This is just the debug data we're going to use to test this thing...
            string username = "TheOneBeast";
            string password = "coolpasswordbrah";
            string email = "gregherman219@hotmail.com";
            string proxy = "119.252.160.34:8080";

            createAccount(username, password, email, proxy);
        }

        public void createAccount(string username, string password, string email, string proxy)
        {
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
            string thePage;

            //This will store the page for HTMLAgilityPack
            HtmlAgilityPack.HtmlDocument agilityPage;

            //These are used to properly encode our post data
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
                //request.Proxy = proxyObject;

                postRequestStream = request.GetRequestStream();
                postRequestStream.Write(byteData, 0, byteData.Length);
                postRequestStream.Close();

                //Grab the response
                response = (HttpWebResponse)request.GetResponse();

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

                postData = email; //change the email here to a variable

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
                //request.Proxy = proxyObject;

                postRequestStream = request.GetRequestStream();
                postRequestStream.Write(byteData, 0, byteData.Length);
                postRequestStream.Close();

                //Grab the response
                //response = (HttpWebResponse)request.GetResponse();

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

                // extract hrefs
                List<string> hrefTags = new List<string>();
                foreach (HtmlNode link in agilityPage.DocumentNode.SelectNodes("//a[@href]"))
                {
                    HtmlAttribute att = link.Attributes["href"];
                    hrefTags.Add(att.Value);
                }

                string regURL = "";
                if (hrefTags[0] != null)
                    regURL = hrefTags[0];

                /* STEP 5: Redirect user to homepage */

                //GET http://fiverr.com/?new_registration=i-qc9h5GzVlwo HTTP/1.1
                //Host: fiverr.com
                //User-Agent: Mozilla/5.0 (Windows NT 6.1; WOW64; rv:19.0) Gecko/20100101 Firefox/19.0
                //Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8
                //Accept-Language: en-US,en;q=0.5
                //Accept-Encoding: gzip, deflate
                //Referer: http://fiverr.com/join
                //Cookie: _fiverr_session=BAh7CCIPc2Vzc2lvbl9pZEkiJWI1NDQyNmM0MzY4MjIyYzIwYTY4NmJlN2NlNzc5OWUxBjoGRVQiEF9jc3JmX3Rva2VuSSIxSmJ6R2dFOTg1UEhNU3BpNXZvRkhxbFYzUEhBVXpMelFOT3FWSnQzSGhPUT0GOwBGSSIVdXNlcl9jcmVkZW50aWFscwY7AEZJIgGANjEyMmYyOTdjNDU0ZGRkMjEyNmNmNmFlMGRjNTdkNzkzODg0ZGFhODkwM2M5OWEzMGY1Njk2YmZhNDJlYmQyMDE0MTY4NThlZDY0NjRiZTczMmM4ZjYyZGE1YjE2NThkNWI1ZjhhYzkzZTQxNzU3NWI3NDQwM2JmY2M2ODA2N2MGOwBU--ddc44251c7c4ac35c0becaac407fd05db4d02307; __utma=156287140.1399514973.1361481945.1361481945.1361481945.1; __utmb=156287140.1.10.1361481945; __utmc=156287140; __utmz=156287140.1361481945.1.1.utmcsr=(direct)|utmccn=(direct)|utmcmd=(none); fiverr_xauth=6122f297c454ddd2126cf6ae0dc57d793884daa8903c99a30f5696bfa42ebd201416858ed6464be732c8f62da5b1658d5b5f8ac93e417575b74403bfcc68067c%3A%3A
                //Connection: keep-alive

                request = (HttpWebRequest)HttpWebRequest.Create(regURL);

                request.Method = "GET";
                request.Host = "fiverr.com";
                request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:19.0) Gecko/20100101 Firefox/19.0";
                request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
                request.Headers.Add("Accept-Language: en-US,en;q=0.5");
                request.Headers.Add("Accept-Encoding: gzip, deflate");
                request.Referer = "http://fiverr.com/join";
                request.CookieContainer = tempCookies;
                request.KeepAlive = true;
                request.Proxy = proxyObject;

                //Grab the response and store the cookies
                response = (HttpWebResponse)request.GetResponse();
                tempCookies.Add(response.Cookies);
            }
            catch (WebException wex)
            {

            }
            catch (Exception ex)
            {

            }
        }
    }
}
