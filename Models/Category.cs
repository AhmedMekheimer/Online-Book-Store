﻿namespace Online_Book_Store.Models
{
    public class Category
    {
        public int Id { get; set; }
        [Required]
        [MinLength(3)]
        public string Name { get; set; } = null!;
        [MinLength(20)]
        [MaxLength(5000)]
        public string? Description { get; set; }
        [ValidateNever]
        public List<Book> Books { get; set; } = new List<Book>();
        [ValidateNever]
        public ICollection<CategoryFile> Files { get; set; } = new List<CategoryFile>();
    }
}
