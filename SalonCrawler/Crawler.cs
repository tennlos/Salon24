﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        private const string UserPage = "http://www.salon24.pl/catalog/1,1,CountPosts,2";

        private readonly ISession _session;
        private readonly int _maxUsers;
        private readonly int _maxPages;

        private readonly Dictionary<string, int> _categoryDict = new Dictionary<string, int>();
        private readonly Dictionary<string, Newspaper> _currentNewspapers = new Dictionary<string, Newspaper>();
        private readonly Dictionary<string, Tag> _currentTags = new Dictionary<string, Tag>();

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
                    _session.Flush();
                    ++idCounter;
                }
            }
            catch (Exception e)
            {
                Logger.Log(e, "Categories");
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
                Logger.Log(e, newCategory);
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
                var leftside =
                    from input in doc.DocumentNode.Descendants("ul")
                    where input.Attributes["class"] != null && input.Attributes["class"].Value == "author-list-2cols-left"
                    select input;
                ProcessUser(leftside);
                var rightside =
                    from input in doc.DocumentNode.Descendants("ul")
                    where input.Attributes["class"] != null && input.Attributes["class"].Value == "author-list-2cols-right"
                    select input;
                ProcessUser(rightside);
                
            }
            catch (Exception e)
            {
                Logger.Log(e, "Users");
            }
        }

        private void ProcessUser(IEnumerable<HtmlNode> tcontent)
        {
            var userCounter = 0;
            var content = from input in tcontent.First().Descendants("a") select input;
            foreach (var node in content)
            {
                Logger.Log("Processing user started.");
                var user = new User
                {
                    Nick = node.InnerText,
                    Address = node.Attributes["href"].Value,
                };
                var existing = _session.CreateCriteria<User>().Add(Restrictions.Eq("Nick", user.Nick)).List<User>().FirstOrDefault();
                if (existing != null)
                    user = existing;
                else
                    _session.Save(user);
                Logger.Log("Nick: " + user.Nick);
                GetUserInfo(user, false);
                Logger.Log("Saving user...");
                _session.SaveOrUpdate(user);
                _session.Flush();
                Logger.Log("User saved!");
                ++userCounter;
                if (userCounter == _maxUsers)
                    break;
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
                    user.Type = 2;
                }
                else
                {
                    user.AboutMe = WebUtility.HtmlDecode(CrawlerHelper.GetStringValueByClass(content, "author-about-desc"));
                    user.PostCount = Convert.ToInt32(CrawlerHelper.GetStringValueByClass(content, "with-icon author-posts"));
                    user.CommentCount = Convert.ToInt32(CrawlerHelper.GetStringValueByClass(content, "with-icon author-comments"));
                    user.Description = WebUtility.HtmlDecode(CrawlerHelper.GetStringValueById(doc.DocumentNode, "blog-header-title"));
                    if (CrawlerHelper.GetNodeByClass(doc.DocumentNode, "theme_1 ") != null)
                        user.Type = 0;
                    else if (CrawlerHelper.GetNodeByClass(doc.DocumentNode, "theme_2 ") != null)
                        user.Type = 1;
                    else
                        user.Type = 2;
                }
                user.LastUpdatedOn = DateTime.Now;

                if (!basic)
                {
                    _currentNewspapers.Clear();
                    _currentTags.Clear();
                }

                user.Posts = basic ? new List<Post>() : GetPostsForPage(doc, user, 1);
            }
            catch (Exception e)
            {
                Logger.Log(e, user);
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
                    var date = Utils.ParseDate(CrawlerHelper.GetStringValueByClass(post.ParentNode, "post-created"));
                    Logger.Log("Processing post started.");
                    Logger.Log("Address: " + address);
                    lastNode = address;
                    var newPost = new Post { User = user, Address = address, Date = date };
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
                var encodedTitle = CrawlerHelper.GetStringValueByClass(doc.DocumentNode, "cqi_s_no cqi_t_post cqi_oid_" + sitePostId);
                if (encodedTitle == null)
                {
                    newPost.Title = "*** BRAK DOSTĘPU ***";
                    newPost.LastUpdatedOn = DateTime.Now;
                    newPost.Comments = new List<Comment>();
                }
                else
                {
                    newPost.Title = WebUtility.HtmlDecode(CrawlerHelper.GetStringValueByClass(doc.DocumentNode, "cqi_s_no cqi_t_post cqi_oid_" + sitePostId));
                    var postNode = CrawlerHelper.GetNodeByClass(doc.DocumentNode, "post");
                    var categoryCode = CrawlerHelper.GetStringValueByTagAndClass(postNode, "span", "sep").Split(':')[1].Substring(1);
                    newPost.Category = _session.Get<Category>(_categoryDict[categoryCode]);
                    newPost.CommentCount = Convert.ToInt32(CrawlerHelper.GetStringValueByTagAndClass(postNode, "span", "sep", 1).Split(' ')[0]);
                    newPost.PostContent = WebUtility.HtmlDecode(GetContent(postNode, userAddress));
                    GetTags(CrawlerHelper.GetNodeByClass(postNode, "post-tags"), newPost);
                    newPost.LastUpdatedOn = DateTime.Now;

                    newPost.Comments = new List<Comment>();
                    newPost.Comments = GetComments(doc, newPost);
                    newPost.Newspapers = GetNewspapers(doc, newPost);
                    newPost.Links = GetLinksForPost(newPost);
                }
            }
            catch (Exception e)
            {
                Logger.Log(e, newPost);
            }
        }

        private IList<Link> GetLinksForPost(Post post)
        {
            return GetLinks(post.PostContent);
        }

        private IList<Link> GetLinksForComment(Comment comment)
        {
            return GetLinks(comment.CommentContent);
        }

        private IList<Link> GetLinks(string content)
        {
            var links = new List<Link>();

            Match m = Regex.Match(content, @"http://([\w+?\.\-\w+]+)[^\s]+");

            while (m.Success)
            {
                var link = new Link();
                link.URL = m.Value;
                link.Domain = m.Groups[1].Value;
                links.Add(link);
                m = m.NextMatch();
            }

            return links;
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
                var nameNode = CrawlerHelper.GetNodeById(newspaperContent.DocumentNode, "newspaper-header");
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
                    newspapers.Add(newspaperForName);
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

        private void GetTags(HtmlNode tagsNode, Post post)
        {
            Logger.Log("Getting tags for the post.");

            post.Tags = new List<Tag>();
            var list =  CrawlerHelper.GetAllStringValuesByTag(tagsNode, "strong");
            foreach (var tag in list)
            {
                var fixedTag = WebUtility.HtmlDecode(tag);
                var foundTag = _session.CreateCriteria<Tag>().Add(Restrictions.Eq("Name", fixedTag)).List<Tag>().FirstOrDefault();
                if (foundTag != null)
                {
                    post.Tags.Add(foundTag);
                }
                else
                {
                    if (_currentTags.ContainsKey(fixedTag))
                    {
                        var existing = _currentTags[fixedTag];
                        //existing.Posts.Add(newPost);
                        post.Tags.Add(existing);
                    }
                    else
                    {
                        var newTag = new Tag
                        {
                            Name = fixedTag
                        };
                        post.Tags.Add(newTag);
                        _currentTags[fixedTag] = newTag;
                    }
                    
                }
            }
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
            newComment.Links = GetLinksForComment(newComment);
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
                Logger.Log(ex, "HtmlDocument");
                return null;
            }
            
        }
    }
}
