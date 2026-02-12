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
        public decimal TotalPrice { get; set; } 
        public DateTime CreatedAt { get; set; }
        public int PickupPointId { get; set; }
        public List<OrderItem> Items { get; set; }
    }
}

