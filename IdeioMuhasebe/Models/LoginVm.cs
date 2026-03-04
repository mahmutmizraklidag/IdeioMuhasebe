using System.ComponentModel.DataAnnotations;

namespace IdeioMuhasebe.Models
{
    public class LoginVm
    {
        [Required]
        [StringLength(30)]
        public string Username { get; set; } = null!;

        [Required]
        [DataType(DataType.Password)]
        [StringLength(30)]
        public string Password { get; set; } = null!;
    }
}