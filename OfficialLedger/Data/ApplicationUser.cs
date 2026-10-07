using Microsoft.AspNetCore.Identity;

namespace OfficialLedger.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        public Guid ApplicationUserID { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        [System.ComponentModel.DataAnnotations.StringLength(500)]
        public string? Address { get; set; }
        public decimal MileageRate { get; set; } = 0.67m;

        public ApplicationUser()
        {
            ApplicationUserID = Guid.NewGuid();
        }

        public ApplicationUser(string? firstName, string? lastName, decimal mileageRate)
        {
            ApplicationUserID = Guid.NewGuid();
            FirstName = firstName;
            LastName = lastName;
            MileageRate = mileageRate;
        }
    }

}
