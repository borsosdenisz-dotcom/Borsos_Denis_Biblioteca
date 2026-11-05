using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Lab1_MVC.Data;
using Lab1_MVC.Models;

namespace Lab1_MVC.Controllers
{
    public class OrdersController : Controller
    {
        private readonly LibraryContext _context;

        public OrdersController(LibraryContext context)
        {
            _context = context;
        }

        // GET: Orders
        public async Task<IActionResult> Index(int? customerId)
        {
            var orders = _context.Order
                .Include(o => o.Book)
                .Include(o => o.Customer)
                .AsNoTracking();

            if (customerId != null)
                orders = orders.Where(o => o.CustomerID == customerId);

            ViewData["CustomerFilter"] = new SelectList(
                _context.Customer.OrderBy(c => c.Name), "CustomerID", "Name", customerId);

            return View(await orders.OrderByDescending(o => o.OrderDate)
                                    .ThenByDescending(o => o.OrderID)
                                    .ToListAsync());
        }

        // GET: Orders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Order
                .Include(o => o.Book)
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(m => m.OrderID == id);
            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // GET: Orders/Create
        public IActionResult Create()
        {
            PopulateDropDowns();
            return View(new OrderFormViewModel { OrderDate = DateTime.Today });
        }

        // POST: Orders/Create
        // Overposting: se leagă doar câmpurile din OrderFormViewModel (fără OrderID, fără navigări).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OrderFormViewModel vm)
        {
            if (ModelState.IsValid)
            {
                var order = new Order
                {
                    CustomerID = vm.CustomerID,
                    BookID = vm.BookID,
                    OrderDate = vm.OrderDate
                };
                _context.Add(order);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            PopulateDropDowns(vm.CustomerID, vm.BookID);
            return View(vm);
        }

        // GET: Orders/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Order.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }
            PopulateDropDowns(order.CustomerID, order.BookID);
            return View(new OrderFormViewModel
            {
                OrderID = order.OrderID,
                CustomerID = order.CustomerID,
                BookID = order.BookID,
                OrderDate = order.OrderDate
            });
        }

        // POST: Orders/Edit/5
        // Overposting: entitatea se încarcă din bază și se copiază doar câmpurile permise din ViewModel.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, OrderFormViewModel vm)
        {
            if (id != vm.OrderID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var order = await _context.Order.FindAsync(id);
                if (order == null)
                {
                    return NotFound();
                }
                order.CustomerID = vm.CustomerID;
                order.BookID = vm.BookID;
                order.OrderDate = vm.OrderDate;
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            PopulateDropDowns(vm.CustomerID, vm.BookID);
            return View(vm);
        }

        private void PopulateDropDowns(object? customerId = null, object? bookId = null)
        {
            ViewData["CustomerID"] = new SelectList(_context.Customer.OrderBy(c => c.Name), "CustomerID", "Name", customerId);
            ViewData["BookID"] = new SelectList(_context.Book.OrderBy(b => b.Title), "ID", "Title", bookId);
        }

        // GET: Orders/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Order
                .Include(o => o.Book)
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(m => m.OrderID == id);
            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // POST: Orders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var order = await _context.Order.FindAsync(id);
            if (order != null)
            {
                _context.Order.Remove(order);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool OrderExists(int id)
        {
            return _context.Order.Any(e => e.OrderID == id);
        }
    }
}
