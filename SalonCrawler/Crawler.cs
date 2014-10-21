using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        private readonly ISession _session;
        private readonly int _maxUsers;
        private readonly int _maxPages;

        private readonly Dictionary<string, int> _categoryDict = new Dictionary<string, int>();
        private readonly Dictionary<string, Newspaper> _currentNewspapers = new Dictionary<string, Newspaper>();

        public Crawler(ISession session, int maxUsers, int maxPages)
        {
            _session = session;
            _maxUsers = maxUsers;
            _maxPages = maxPages;
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
            if (doc == null)
                return;

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
            if (doc == null)
                return;

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
            if (doc == null)
                return;

            try
            {
                var tcontent =
                    from input in doc.DocumentNode.Descendants("ul")
                    where input.Attributes["class"] != null && input.Attributes["class"].Value == "author-list-2cols-left"
                    select input;
                var userCounter = 0;
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
                    Logger.Log("Saving user...");
                    _session.Save(user);
                    _session.Flush();
                    Logger.Log("User saved!");
                    ++userCounter;
                    if (userCounter == _maxUsers)
                        break;
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
            if (doc == null)
                return;

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
                    user.AboutMe = WebUtility.HtmlDecode(CrawlerHelper.GetStringValueByClass(content, "author-about-desc"));
                    user.PostCount = Convert.ToInt32(CrawlerHelper.GetStringValueByClass(content, "with-icon author-posts"));
                    user.CommentCount = Convert.ToInt32(CrawlerHelper.GetStringValueByClass(content, "with-icon author-comments"));
                    user.Description = WebUtility.HtmlDecode(CrawlerHelper.GetStringValueById(doc.DocumentNode, "blog-header-title"));
                }
                user.LastUpdatedOn = DateTime.Now;

                if (!basic)
                {
                    _currentNewspapers.Clear();
                }

                user.Posts = basic ? new List<Post>() : GetPostsForPage(doc, user, 1);
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        private IList<Post> GetPostsForPage(HtmlDocument doc, User user, int page)
        {
            Logger.Log("Getting posts for the user. Page = " + page);

            if (page > 1)
            {
                var pagenode = CrawlerHelper.GetNodeByClass(doc.DocumentNode, "pages");
                if (pagenode != null && page <= _maxPages)
                {
                    var nextpage = CrawlerHelper.GetNodeByClass(pagenode, "pages_right");
                    if (nextpage == null)
                        return new List<Post>();
                    doc = GetHtmlDocument(WebRequest.Create(user.Address + nextpage.Attributes["href"].Value));
                    if (doc == null)
                        return new List<Post>();
                }
                else
                    return new List<Post>();
            }       

            var postList = new List<Post>();
            var posts = CrawlerHelper.GetNodeByClass(doc.DocumentNode, "post-list");
            foreach (var post in posts.Descendants("h2"))
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
                    var newPost = new Post { User = user, Address = address };
                    GetPostInfo(newPost, address, user.Address);
                    postList.Add(newPost);
                }
            }
            var addlist = GetPostsForPage(doc, user, ++page);
            if (addlist.Count > 0)
                postList.AddRange(addlist);
            return postList;
        }

        private void GetPostInfo(Post newPost, string address, string userAddress)
        {
            Logger.Log("Getting post info.");

            var request = WebRequest.Create(address);
            var doc = GetHtmlDocument(request);
            if (doc == null)
                return;

            try
            {
                var sitePostId = address.Split('/')[3].Split(',')[0];
                newPost.Title = WebUtility.HtmlDecode(CrawlerHelper.GetStringValueByClass(doc.DocumentNode, "cqi_s_no cqi_t_post cqi_oid_" + sitePostId));
                var dateString = CrawlerHelper.GetStringValueByClass(doc.DocumentNode, "created");
                newPost.Date = Utils.ParseDate(dateString);
                var postNode = CrawlerHelper.GetNodeByClass(doc.DocumentNode, "post");
                var categoryCode = CrawlerHelper.GetStringValueByTagAndClass(postNode, "span", "sep").
                    Split(':')[1].Substring(1);
                newPost.Category = _session.Get<Category>(_categoryDict[categoryCode]);
                newPost.CommentCount = Convert.ToInt32(
                    CrawlerHelper.GetStringValueByTagAndClass(postNode, "span", "sep", 1).Split(' ')[0]);
                newPost.PostContent = WebUtility.HtmlDecode(GetContent(postNode, userAddress));
                newPost.Tags = GetTags(CrawlerHelper.GetNodeByClass(postNode, "post-tags"));
                newPost.LastUpdatedOn = DateTime.Now;

                newPost.Comments = new List<Comment>();
                newPost.Comments = GetComments(doc, newPost);
                newPost.Newspapers = GetNewspapers(doc, newPost);

            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        private IList<Newspaper> GetNewspapers(HtmlDocument doc, Post newPost)
        {
            var newspapers = new List<Newspaper>();
            var newspapersNode = CrawlerHelper.GetNodeByClass(doc.DocumentNode, "post-newspapers");
            if (newspapersNode == null)
                return newspapers;

            Logger.Log("Getting newspapers for the post.");

            foreach (var node in newspapersNode.Descendants("a"))
            {
                if (node.Attributes["href"] == null)
                    continue;
                var address = node.Attributes["href"].Value;
                if (!address.StartsWith("http://lubczasopismo")) // the same link again
                    continue;
                var request = WebRequest.Create(address);
                var newspaperContent = GetHtmlDocument(request);
                if (newspaperContent == null)
                    continue;
                var newspaper = new Newspaper();
                var nameNode = CrawlerHelper.GetNodeByID(newspaperContent.DocumentNode, "newspaper-header");
                var name = WebUtility.HtmlDecode(nameNode.Descendants("a").First().InnerText);
                var newspaperForName = _session.CreateCriteria<Newspaper>()
                    .Add(Restrictions.Eq("Name", name)).List<Newspaper>().FirstOrDefault();
                if (newspaperForName == null)
                {
                    if (_currentNewspapers.ContainsKey(name))
                    {
                        var existing = _currentNewspapers[name];
                        existing.Posts.Add(newPost);
                        newspapers.Add(existing);
                    }
                    else
                    {
                        newspaper.Name = name;
                        GetNewspaperInfo(newspaper, newspaperContent.DocumentNode, newPost);
                        _currentNewspapers[name] = newspaper;
                        newspapers.Add(newspaper);
                    }
                }
                else
                {
                    newspaperForName.Posts.Add(newPost);
                    newspapers.Add(newspaper);
                    _session.Update(newspaperForName);
                }
                
            }
            return newspapers;
        }

        private void GetNewspaperInfo(Newspaper newNewspaper, HtmlNode newspaperNode, Post newPost)
        {
            newNewspaper.Description = WebUtility.HtmlDecode(CrawlerHelper.GetStringValueById(newspaperNode, "newspaper-slogan"));
            var newsNode = CrawlerHelper.GetNodeByPartialClass(newspaperNode, "author-about-body");
            var userNode = newsNode.Descendants("a").First();
            var nick = userNode.InnerText;
            var address = userNode.Attributes["href"].Value;
            var commentCount = Convert.ToInt32(
                CrawlerHelper.GetStringValueByClass(newsNode, "with-icon author-comments"));
            newNewspaper.User = GetUserForNewspaper(nick, address, commentCount);
            newNewspaper.Posts = new List<Post> { newPost };
        }

        private User GetUserForNewspaper(string nick, string address, int commentCount)
        {
            var user = _session.CreateCriteria<User>().Add(Restrictions.Eq("Nick", nick)).List<User>().FirstOrDefault();
            if (user != null)
            {
                return user;
            }
            Logger.Log("Creating user for the newspaper");
            Logger.Log("Nick: " + nick);
            user = new User
            {
                Nick = nick,
                Address = address,
                CommentCount = commentCount
            };
            GetUserInfo(user, true);
            _session.Save(user);
            return _session.Get<User>(user.Id);
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
                var request = WebRequest.Create(userAddress + pageLinkNode.Attributes["href"].Value);
                var doc = GetHtmlDocument(request);
                if (doc == null)
                    return contentBuilder.ToString();
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
            newComment.Title = WebUtility.HtmlDecode(CrawlerHelper.GetStringValueByTag(commentNode, "h3"));
            newComment.CommentContent = WebUtility.HtmlDecode(CrawlerHelper.GetStringValueByClass(commentNode, "comment-body"));
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
            user = new User
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
            try
            {
                var response = request.GetResponse();
                var data = response.GetResponseStream();
                var doc = new HtmlDocument();

                string html;
                using (var sr = new StreamReader(data))
                {
                    html = sr.ReadToEnd();
                }
                doc.LoadHtml(html);
                return doc;
            
            }
            catch (WebException ex) 
            {
                Logger.Log(ex);
                return null;
            }
            
        }
    }
}
