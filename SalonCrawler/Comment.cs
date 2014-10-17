using System;
using System.Text;
using System.Collections.Generic;


namespace SalonCrawler {
    
    public partial class Comment {
        public virtual int Id { get; set; }
        public virtual User User { get; set; }
        public virtual Post Post { get; set; }
        public virtual string Title { get; set; }
        public virtual string CommentContent { get; set; }
        public virtual DateTime CreationDate { get; set; }
    }
}
