using System;
using System.Collections.Generic;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public class PorterDeliveryService
    {
        private readonly PorterDeliveryRepository _repository = new PorterDeliveryRepository();
        public List<PorterDelivery> GetAll() { return _repository.GetAll(); }

        public int Save(PorterDelivery item)
        {
            if (item == null) throw new ArgumentNullException("item");
            SessionManager.DemandPermission("WorkOrders", item.PorterDeliveryId <= 0 ? "Create" : "Edit");
            if (string.IsNullOrWhiteSpace(item.BookingReference)) throw new InvalidOperationException("Enter the booking ID returned by Porter.");
            if (string.IsNullOrWhiteSpace(item.PickupAddress)) throw new InvalidOperationException("Pickup address is required.");
            if (string.IsNullOrWhiteSpace(item.DropAddress)) throw new InvalidOperationException("Drop address is required.");
            if (item.EstimatedAmount < 0 || item.FinalAmount < 0) throw new InvalidOperationException("Delivery amounts cannot be negative.");
            bool isNew = item.PorterDeliveryId <= 0;
            string actor = SessionManager.CurrentUser != null ? SessionManager.CurrentUser.DisplayName : Environment.UserName;
            if (isNew) { item.CreatedAt = DateTime.Now; item.CreatedByName = actor; }
            item.ModifiedAt = DateTime.Now; item.ModifiedByName = actor;
            int id = _repository.Save(item);
            SessionManager.LogAction(isNew ? "Create" : "Edit", "PorterDeliveries", id, "Porter booking " + item.BookingReference);
            return id;
        }
    }
}
