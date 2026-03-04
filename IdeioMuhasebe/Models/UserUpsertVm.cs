using System.ComponentModel.DataAnnotations;

namespace IdeioMuhasebe.Models
{
    public class UserUpsertVm
    {
        public int Id { get; set; }

        [Required]
        [StringLength(30)]
        public string Username { get; set; } = null!;

        // Yeni kullanıcıda zorunlu, düzenlemede opsiyonel (boşsa değişmez)
        [StringLength(30)]
        public string? Password { get; set; }
    }
}