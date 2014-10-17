using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.IO;
using System.Net;

namespace SalonCrawler
{
    public class Crawler
    {
        private const string Home = "http://www.salon24.pl/";
        private const string GetUser = "http://www.salon24.pl/catalog/1,1,CountComments,2";

        public void Crawl()
        {
            StringBuilder sb = new StringBuilder("http://www.salon24.pl/");
            WebRequest request = WebRequest.Create(GetUser);
            var doc = GetHtmlDocument(request);

            try
            {
                var tcontent = from input in doc.DocumentNode.Descendants("ul") 
                                where input.Attributes["class"] != null && input.Attributes["class"].Value == "author-list-2cols-left"
                        select input;
                var content = from input in tcontent.First().Descendants("a") select input;
                foreach (var node in content)
                {
                    User user = new User();
                    user.Nick = node.InnerText;
                    user.Address = node.Attributes["href"].Value;
                    GetUserInfo(user);
                }
                    
            }
            catch
            {
                
            }

            
        }

        private void GetUserInfo(User user)
        {
            WebRequest request = WebRequest.Create(user.Address);
            var doc = GetHtmlDocument(request);
            try
            {
                var tcontent = from input in doc.DocumentNode.Descendants("div") 
                                where input.Attributes["class"] != null && input.Attributes["class"].Value == "author-about-body"
                        select input;
                var content = tcontent.First();
               
                user.AboutMe = CrawlerHelper.GetStringValue(content, "author-about-desc");
                user.PostCount = Convert.ToInt32(CrawlerHelper.GetStringValue(content, "with-icon author-posts"));
                user.CommentCount = Convert.ToInt32(CrawlerHelper.GetStringValue(content, "with-icon author-comments"));
                user.Description = CrawlerHelper.GetStringValueFromId(doc.DocumentNode, "blog-header-title");
                user.LastUpdatedOn = DateTime.Now;

                user.Posts = GetPosts(user, doc);
                    
            }
            catch
            {
                
            }
        }

        private IList<Post> GetPosts(User user,HtmlDocument doc)
        {
            List<Post> postList = new List<Post>();
            var posts = CrawlerHelper.GetNode(doc.DocumentNode, "post-list");
            foreach (var post in posts.DescendantsAndSelf())
            {
                var content = from input in post.Descendants("a") select input;
                foreach (var node in content)
                {
                    Post newPost = new Post();
                    var address = node.Attributes["href"].Value;
                    GetPostInfo(newPost, address);
                    postList.Add(newPost);
                }
            }
            return postList;
        }

        private void GetPostInfo(Post newPost, string address)
        {
            WebRequest request = WebRequest.Create(address);
            var doc = GetHtmlDocument(request);
            try
            {
                newPost.CommentCount = Convert.ToInt32(CrawlerHelper.GetStringValue(doc.DocumentNode, "icon icon-comment"));
                //newPost.Date = Convert.ToDateTime(CrawlerHelper.GetStringValue(doc.DocumentNode, "created"));
                //newPost.Title = ...
            }
            catch
            {

            }
        }

        private HtmlDocument GetHtmlDocument(WebRequest request)
        {
            WebResponse response = request.GetResponse();
            HtmlDocument doc = new HtmlDocument();
            Stream data = response.GetResponseStream();
            string html = String.Empty;
            using (StreamReader sr = new StreamReader(data))
            {
                html = sr.ReadToEnd();
            }
            doc.LoadHtml(html);
            return doc;
        }
    }
}
