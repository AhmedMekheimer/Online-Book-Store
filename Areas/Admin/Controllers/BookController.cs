﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Online_Book_Store.Utility;
using Online_Book_Store.ViewModels.Admin;

namespace Online_Book_Store.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class BookController : Controller
    {
        private readonly IRepository<Book> _bookRepo;
        private readonly IRepository<Category> _catRepo;
        private readonly IRepository<Author> _authRepo;
        private readonly IRepository<PublishingHouse> _pubRepo;
        private readonly IRepository<BookFile> _bfRepo;
        public BookController(IRepository<Book> bookRepo, IRepository<Category> catRepo, IRepository<Author> authRepo, IRepository<PublishingHouse> pubRepo, IRepository<BookFile> bfRepo)
        {
            _bookRepo = bookRepo;
            _authRepo = authRepo;
            _catRepo = catRepo;
            _pubRepo = pubRepo;
            _bfRepo = bfRepo;
        }
        [Authorize(Policy = $"{SD.Workers}")]
        public async Task<IActionResult> Index()
        {
            var books = await _bookRepo.GetAsync(null, new List<Func<IQueryable<Book>, IQueryable<Book>>> { a => a.Include(a => a.Authors) }, false);
            return View(books);
        }
        [Authorize(Policy = $"{SD.Admins}")]
        public async Task<IActionResult> Create()
        {
            var categories = await _catRepo.GetAsync();
            var authors = await _authRepo.GetAsync();
            var pubs = await _pubRepo.GetAsync();
            BookCatAuthPubsVM BookCatAuthPubsVM = new()
            {
                Categories = categories,
                Authors = authors,
                PublishingHouses = pubs,
                Book = new Book()
            };
            return View(BookCatAuthPubsVM);
        }
        [HttpPost]
        [RequestSizeLimit(1_000_000_000)] // 1GB limit
        [Authorize(Policy = $"{SD.Admins}")]
        public async Task<IActionResult> Create(BookDataVM bookDataVM, List<IFormFile> files)
        {
            if (bookDataVM.Book is null)
                return NotFound();

            ModelState.Remove("files");
            if (!ModelState.IsValid)
            {
                foreach (var authId in bookDataVM.AuthorsIds)
                {
                    if ((await _authRepo.GetOneAsync(a => a.Id == authId)) is Author auth)
                    {
                        bookDataVM.Book.Authors.Add(auth);
                    }
                }
                foreach (var pubId in bookDataVM.PublishersIds)
                {
                    if ((await _pubRepo.GetOneAsync(p => p.Id == pubId)) is PublishingHouse pub)
                    {
                        bookDataVM.Book.PublishingHouses.Add(pub);
                    }
                }

                BookCatAuthPubsVM BookCatAuthPubsVM = new()
                {
                    Categories = await _catRepo.GetAsync(),
                    Authors = await _authRepo.GetAsync(),
                    PublishingHouses = await _pubRepo.GetAsync(),
                    Book = bookDataVM.Book
                };
                return View(BookCatAuthPubsVM);
            }


            Book book = new()
            {
                Name = bookDataVM.Book.Name,
                Price = bookDataVM.Book.Price,
                AvailableCopies = bookDataVM.Book.AvailableCopies,
                CategoryId = bookDataVM.Book.CategoryId,
                Files = new List<BookFile>()
            };

            foreach (var file in files)
            {
                //Save File in Physical Storage
                string fileName;
                FileType fileType;
                (fileName, fileType) = FileService.UploadNewFile(file);

                if (fileName is not null && (fileType == FileType.Image || fileType == FileType.Video))
                {
                    // Save File in Db
                    BookFile bookFile = new()
                    {
                        Name = fileName,
                        FileType = fileType
                    };
                    await _bfRepo.CreateAsync(bookFile);

                    //Save File to Book Table
                    book.Files.Add(bookFile);
                }
                else
                    return NotFound();
            }
            foreach (var authId in bookDataVM.AuthorsIds)
            {
                if ((await _authRepo.GetOneAsync(a => a.Id == authId)) is Author auth)
                {
                    book.Authors.Add(auth);
                }
            }
            foreach (var pubId in bookDataVM.PublishersIds)
            {
                if ((await _pubRepo.GetOneAsync(p => p.Id == pubId)) is PublishingHouse pub)
                {
                    book.PublishingHouses.Add(pub);
                }
            }
            var CreateResult=await _bookRepo.CreateAsync(book);
            if(CreateResult)
                TempData["success-notification"] = "Book Created Successfully";
            else
                TempData["error-notification"] = "Book Creation Failure";
            return RedirectToAction(nameof(Index));
        }
        [Authorize(Policy = $"{SD.Admins}")]
        public async Task<IActionResult> Edit(int id)
        {
            Book? book = await _bookRepo.GetOneAsync(b => b.Id == id, new List<Func<IQueryable<Book>, IQueryable<Book>>> {
            a => a.Include(b => b.Authors),
            a => a.Include(b => b.PublishingHouses),
            a => a.Include(b => b.Files)
            });
            if (book is null)
            {
                return NotFound();
            }
            else
            {
                BookCatAuthPubsVM BookCatAuthPubsVM = new()
                {
                    Categories = await _catRepo.GetAsync(),
                    Authors = await _authRepo.GetAsync(),
                    PublishingHouses = await _pubRepo.GetAsync(),
                    Book = new Book()
                };

                BookCatAuthPubsVM.Book = book;
                return View(BookCatAuthPubsVM);
            }
        }

        [HttpPost]
        [RequestSizeLimit(1_000_000_000)] // 1GB limit
        [Authorize(Policy = $"{SD.Admins}")]
        public async Task<IActionResult> Edit(BookDataVM bookDataVM, List<IFormFile> files, List<int> ExistingFilesIds)
        {
            if (bookDataVM.Book is null)
                return NotFound();

            ModelState.Remove("files");
            ModelState.Remove("ExistingFilesIds");
            //Invalid Server Side
            if (!ModelState.IsValid)
            {
                Book? book = await _bookRepo.GetOneAsync(e => e.Id == bookDataVM.Book.Id,
                new List<Func<IQueryable<Book>, IQueryable<Book>>> { a => a.Include(b => b.Files) }, false);

                if (book is null)
                {
                    return NotFound();
                }

                foreach (var authId in bookDataVM.AuthorsIds)
                {
                    if ((await _authRepo.GetOneAsync(a => a.Id == authId)) is Author auth)
                    {
                        book.Authors.Add(auth);
                    }
                }
                foreach (var pubId in bookDataVM.PublishersIds)
                {
                    if ((await _pubRepo.GetOneAsync(p => p.Id == pubId)) is PublishingHouse pub)
                    {
                        book.PublishingHouses.Add(pub);
                    }
                }

                BookCatAuthPubsVM BookCatAuthPubsVM = new()
                {
                    Categories = await _catRepo.GetAsync(),
                    Authors = await _authRepo.GetAsync(),
                    PublishingHouses = await _pubRepo.GetAsync(),
                    Book = new Book()
                };
                book.Name = bookDataVM.Book.Name;
                book.Price = bookDataVM.Book.Price;
                book.AvailableCopies = bookDataVM.Book.AvailableCopies;
                book.CategoryId = bookDataVM.Book.CategoryId;

                BookCatAuthPubsVM.Book = book;

                return View(BookCatAuthPubsVM);
            }

            Book? existingBook = await _bookRepo.GetOneAsync(b => b.Id == bookDataVM.Book.Id, new List<Func<IQueryable<Book>, IQueryable<Book>>> {
            a => a.Include(b => b.Authors),
            a => a.Include(b => b.PublishingHouses),
            a => a.Include(b => b.Files)
            });

            if (existingBook == null) return NotFound();

            Book NewBook = new Book();
            NewBook.Id = bookDataVM.Book.Id;
            NewBook.Name = bookDataVM.Book.Name;
            NewBook.Price = bookDataVM.Book.Price;
            NewBook.AvailableCopies = bookDataVM.Book.AvailableCopies;
            NewBook.CategoryId = bookDataVM.Book.CategoryId;

            // Handle existing files - remove files not in ExistingFilesIds
            var filesToDelete = existingBook.Files
                .Where(f => !ExistingFilesIds.Contains(f.Id))
                .ToList();

            foreach (var file in filesToDelete)
            {
                // Delete physical file
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot\\Files", file.Name);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                // Remove file from database
                // Attach and mark for deletion
                if (!(await _bfRepo.DeleteAsync(file)))
                    return NotFound();
            }

            // Handle Authors by clearing this books' authors from AuthorBook Table
            existingBook.Authors.Clear();
            await _bookRepo.CommitAsync();
            NewBook.Authors = new List<Author>();
            foreach (var authId in bookDataVM.AuthorsIds)
            {
                if ((await _authRepo.GetOneAsync(a => a.Id == authId)) is Author auth)
                {
                    NewBook.Authors.Add(auth);
                }
            }

            // Handle Publishing Houses 
            existingBook.PublishingHouses.Clear();
            await _bookRepo.CommitAsync();
            NewBook.PublishingHouses = new List<PublishingHouse>();
            foreach (var pubId in bookDataVM.PublishersIds)
            {
                if ((await _pubRepo.GetOneAsync(p => p.Id == pubId)) is PublishingHouse pub)
                {
                    NewBook.PublishingHouses.Add(pub);
                }
            }

            //Upload New Files
            foreach (var file in files)
            {
                //Save File in Physical Storage
                string fileName;
                FileType fileType;
                (fileName, fileType) = FileService.UploadNewFile(file);

                if (fileName is not null && (fileType == FileType.Image || fileType == FileType.Video))
                {
                    // Save File in Db
                    BookFile bookFile = new()
                    {
                        Name = fileName,
                        FileType = fileType
                    };
                    bookFile.BookId = existingBook.Id;
                    await _bfRepo.CreateAsync(bookFile);
                }
            }

            //Remove Connection with Existing Book
            _bookRepo.DetachEntity(existingBook);

            var UpdateResult=await _bookRepo.UpdateAsync(NewBook);
            if (UpdateResult)
                TempData["success-notification"] = "Book Updated Successfully";
            else
                TempData["error-notification"] = "Book Updating Failure";

            return RedirectToAction(nameof(Index));
        }
        [Authorize(Policy = $"{SD.Admins}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (await (_bookRepo.GetOneAsync(b => b.Id == id, new List<Func<IQueryable<Book>, IQueryable<Book>>> { a => a.Include(b => b.Files) })) is Book book)
            {
                foreach (var file in book.Files)
                {
                    // Delete all files Physically
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot\\Files", file.Name);
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }

                // Delete Book from Db along with its Files
                var DeleteResult=await _bookRepo.DeleteAsync(book);
                if (DeleteResult)
                    TempData["success-notification"] = "Book Removed Successfully";
                else
                    TempData["error-notification"] = "Book Removal Failure";
                return RedirectToAction(nameof(Index));
            }
            return NotFound();
        }
    }
}
