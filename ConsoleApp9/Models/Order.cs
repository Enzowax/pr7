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
        public int UserId { get; set; }
        public int PickupPointId { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; }
        public string PickupPointName { get; set; }
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();
    }
}
