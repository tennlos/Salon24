﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.IO;
using System.Net;
using NHibernate;
using NHibernate.Criterion;

namespace SalonCrawler
{
    public class Crawler
    {
        private const string HomePage = "http://www.salon24.pl/";
        private const string UserPage = "http://www.salon24.pl/catalog/1,1,CountComments,2";
        private readonly int _postsPerUser;
        private readonly ISession _session;

        private readonly Dictionary<string, int> _categoryDict = new Dictionary<string, int>(); 

        public Crawler(ISession session, int postsPerUser)
        {
            _session = session;
            _postsPerUser = postsPerUser;
        }

        public void Crawl()
        {
            Logger.Log("Crawling started.");

            CrawlCategories();
            CrawlUsers();
        }

        private void CrawlCategories()
        {
            Logger.Log("Crawling categories...");

            var request = WebRequest.Create(HomePage);
            var doc = GetHtmlDocument(request);

            try
            {
                var categoriesDropdown = doc.DocumentNode.Descendants("ul").
                    FirstOrDefault(ul => ul.Attributes["class"] != null && ul.Attributes["class"].Value == "dropdown");
                if (categoriesDropdown == null) 
                    return;
                var categories = categoriesDropdown.Descendants("li");
                var firstOmitted = false;
                var idCounter = 1;
                foreach (var categoryNode in categories)
                {
                    if (!firstOmitted)
                    {
                        firstOmitted = true;
                        continue;
                    }
                    Logger.Log("Processing category started.");
                    var category = new Category
                    {
                        Id = idCounter,
                        Code = CrawlerHelper.GetStringValueByTag(categoryNode, "a")
                    };
                    _categoryDict[category.Code] = idCounter;
                    Logger.Log("Code: " + category.Code);
                    var firstOrDefault = categoryNode.Descendants("a").FirstOrDefault();
                    if (firstOrDefault == null) 
                        continue;
                    var address = firstOrDefault.Attributes["href"].Value;
                    GetCategoryInfo(category, address);
                    _session.Save(category);
                    ++idCounter;
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        private void GetCategoryInfo(Category newCategory, string address)
        {
            Logger.Log("Getting category info.");

            var request = WebRequest.Create(address);
            var doc = GetHtmlDocument(request);

            try
            {
                newCategory.Name = CrawlerHelper.GetStringValueByClass(doc.DocumentNode, "page-title");
                newCategory.Posts = new List<Post>();
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        private void CrawlUsers()
        {
            Logger.Log("Crawling users...");

            var request = WebRequest.Create(UserPage);
            var doc = GetHtmlDocument(request);

            try
            {
                var tcontent =
                    from input in doc.DocumentNode.Descendants("ul")
                    where input.Attributes["class"] != null && input.Attributes["class"].Value == "author-list-2cols-left"
                    select input;
                var content = from input in tcontent.First().Descendants("a") select input;
                foreach (var node in content)
                {
                    Logger.Log("Processing user started.");
                    var user = new User
                    {
                        Nick = node.InnerText,
                        Address = node.Attributes["href"].Value
                    };
                    Logger.Log("Nick: " + user.Nick);
                    GetUserInfo(user, false);
                    _session.Save(user);
                    break; // TODO temporary to stop crawling after 1 user
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        private void GetUserInfo(User user, bool basic)
        {
            Logger.Log("Getting user info.");

            var request = WebRequest.Create(user.Address);
            var doc = GetHtmlDocument(request);

            try
            {
                var tcontent = 
                    from input in doc.DocumentNode.Descendants("div")
                    where input.Attributes["class"] != null && input.Attributes["class"].Value == "author-about-body"
                    select input;
                var content = tcontent.First();

                if (user.Address.StartsWith("http://salon24.pl"))
                {
                    user.AboutMe = String.Empty;
                    user.PostCount = 0;
                    user.Description = String.Empty;
                }
                else
                {
                    user.AboutMe = CrawlerHelper.GetStringValueByClass(content, "author-about-desc");
                    user.PostCount = Convert.ToInt32(CrawlerHelper.GetStringValueByClass(content, "with-icon author-posts"));
                    user.CommentCount = Convert.ToInt32(CrawlerHelper.GetStringValueByClass(content, "with-icon author-comments"));
                    user.Description = CrawlerHelper.GetStringValueById(doc.DocumentNode, "blog-header-title");
                }
                user.LastUpdatedOn = DateTime.Now;

                user.Posts = basic ? new List<Post>() : GetPosts(doc, user);
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        private IList<Post> GetPosts(HtmlDocument doc, User user)
        {
            Logger.Log("Getting posts for the user.");

            var postList = new List<Post>();
            var posts = CrawlerHelper.GetNodeByClass(doc.DocumentNode, "post-list");
            var counter = 1;
            foreach (var post in posts.DescendantsAndSelf())
            {
                var content = from input in post.Descendants("a") select input;
                string lastNode = null;
                foreach (var node in content)
                {
                    var address = node.Attributes["href"].Value;
                    if (lastNode != null && address.StartsWith(lastNode)) // the same link again
                        continue;
                    Logger.Log("Processing post started.");
                    Logger.Log("Address: " + address);
                    lastNode = address;
                    var newPost = new Post { User = user };
                    GetPostInfo(newPost, address, user.AboutMe);
                    postList.Add(newPost);
                    ++counter;
                    if (counter > _postsPerUser)
                        break;
                }
                if (counter > _postsPerUser)
                    break;
            }
            return postList;
        }

        private void GetPostInfo(Post newPost, string address, string userAddress)
        {
            Logger.Log("Getting post info.");

            var request = WebRequest.Create(address);
            var doc = GetHtmlDocument(request);

            try
            {
                var sitePostId = address.Split('/')[3].Split(',')[0];
                newPost.Title = CrawlerHelper.GetStringValueByClass(doc.DocumentNode, "cqi_s_no cqi_t_post cqi_oid_" + sitePostId);
                var dateString = CrawlerHelper.GetStringValueByClass(doc.DocumentNode, "created");
                newPost.Date = Utils.ParseDate(dateString);
                var postNode = CrawlerHelper.GetNodeByClass(doc.DocumentNode, "post");
                var categoryCode = CrawlerHelper.GetStringValueByTagAndClass(postNode, "span", "sep").
                    Split(':')[1].Substring(1);
                newPost.Category = _session.Get<Category>(_categoryDict[categoryCode]);
                newPost.CommentCount = Convert.ToInt32(
                    CrawlerHelper.GetStringValueByTagAndClass(postNode, "span", "sep", 1).Split(' ')[0]);
                newPost.PostContent = GetContent(postNode, userAddress);
                newPost.Tags = GetTags(CrawlerHelper.GetNodeByClass(postNode, "post-tags"));
                newPost.LastUpdatedOn = DateTime.Now;

                newPost.Comments = GetComments(doc, newPost);
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        private string GetTags(HtmlNode tagsNode)
        {
            Logger.Log("Getting tags for the post.");

            var list = CrawlerHelper.GetAllStringValuesByTag(tagsNode, "strong");
            var tagString = new StringBuilder();
            foreach (var tag in list)
            {
                tagString.Append(tag).Append(", ");
            }
            if (tagString.Length > 0)
            {
                tagString.Remove(tagString.Length - 2, 2);
            }
            return tagString.ToString();
        }

        private string GetContent(HtmlNode postNode, string userAddress)
        {
            Logger.Log("Getting content for the post.");

            var contentBuilder = new StringBuilder();
            contentBuilder.Append(GetContentPart(postNode));

            var pageLinkNodes = CrawlerHelper.GetAllNodesByTagAndClass(postNode, "a", "pages_pos");
            foreach (var pageLinkNode in pageLinkNodes)
            {
                var request = WebRequest.Create(userAddress + pageLinkNode.Attributes["href"]);
                var doc = GetHtmlDocument(request);
                var pagePostNode = CrawlerHelper.GetNodeByClass(doc.DocumentNode, "post");
                contentBuilder.Append(GetContentPart(pagePostNode));
            }

            return contentBuilder.ToString();
        }

        private string GetContentPart(HtmlNode postNode)
        {
            var textRootNode = CrawlerHelper.GetNodeByClass(postNode, "bbtext");
            var paragraphs = CrawlerHelper.GetAllStringValuesByTag(textRootNode, "p");
            var contentPartBuilder = new StringBuilder();
            foreach (var paragraph in paragraphs)
            {
                contentPartBuilder.Append(WebUtility.HtmlDecode(paragraph)).Append("\n");
            }
            return contentPartBuilder.ToString();
        }

        private IList<Comment> GetComments(HtmlDocument doc, Post post)
        {
            Logger.Log("Getting comments for the post.");

            var commentList = new List<Comment>();
            var comments = CrawlerHelper.GetAllNodesByTagAndClass(doc.DocumentNode, "li", "comment ");
            foreach (var commentNode in comments)
            {
                var comment = new Comment { Post = post };
                GetCommentInfo(comment, commentNode);
                commentList.Add(comment);
            }
            return commentList;
        }

        private void GetCommentInfo(Comment newComment, HtmlNode commentNode)
        {
            newComment.Title = CrawlerHelper.GetStringValueByTag(commentNode, "h3");
            newComment.CommentContent = CrawlerHelper.GetStringValueByClass(commentNode, "comment-body");
            var dateString = CrawlerHelper.GetStringValueByClass(commentNode, "sep");
            newComment.CreationDate = Utils.ParseDate(dateString);
            var userNick = CrawlerHelper.GetStringValueByPartialClass(commentNode, "author-nick");
            var userAddress = CrawlerHelper.GetNodeByPartialClass(commentNode, "author-nick").Attributes["href"].Value;
            var commentCount = Convert.ToInt32(
                CrawlerHelper.GetStringValueByClass(commentNode, "with-icon author-comments"));
            newComment.User = GetUserForComment(userNick, userAddress, commentCount);
        }

        private User GetUserForComment(string nick, string address, int commentCount)
        {
            var user = _session.CreateCriteria<User>().Add(Restrictions.Eq("Nick", nick)).List<User>().FirstOrDefault();
            if (user != null)
            {
                return user;
            }
            Logger.Log("Creating user for the comment");
            Logger.Log("Nick: " + nick);
            user = new User()
            {
                Nick = nick,
                Address = address,
                CommentCount = commentCount
            };
            GetUserInfo(user, true);
            _session.Save(user);
            return _session.Get<User>(user.Id);
        }

        private HtmlDocument GetHtmlDocument(WebRequest request)
        {
            var response = request.GetResponse();
            var doc = new HtmlDocument();
            var data = response.GetResponseStream();
            string html;
            using (var sr = new StreamReader(data))
            {
                html = sr.ReadToEnd();
            }
            doc.LoadHtml(html);
            return doc;
        }
    }
}
