    using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp9.Models
{
    public class Order
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int PVZId { get; set; }
        public DateTime Date { get; set; }
    }
}
