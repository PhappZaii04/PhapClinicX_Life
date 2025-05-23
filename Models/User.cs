﻿using System;
using System.Collections.Generic;

namespace PhapClinicX.Models;

public partial class User
{
    public int UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string? Email { get; set; }

    public string? PasswordHash { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public int? RoleId { get; set; }

    public string? ProfileImage { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? Username { get; set; }

    public virtual ICollection<BlogComment> BlogComments { get; set; } = new List<BlogComment>();

    public virtual ICollection<Blog> Blogs { get; set; } = new List<Blog>();

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<ProductComment> ProductComments { get; set; } = new List<ProductComment>();

    public virtual Role? Role { get; set; }

    public virtual ICollection<ServiceReview> ServiceReviews { get; set; } = new List<ServiceReview>();

    public virtual ICollection<DoctorAppointment> DoctorAppointments { get; set; } = new List<DoctorAppointment>();
}
