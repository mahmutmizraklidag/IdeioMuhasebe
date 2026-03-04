using System.ComponentModel.DataAnnotations;

namespace IdeioMuhasebe.Entities
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Kullanıcı Adı")]
        [Required(ErrorMessage = "Kullanıcı adı boş geçilemez!")]
        [StringLength(30, ErrorMessage = "Kullanıcı adı en fazla 30 karakter olabilir.")]
        public string Username { get; set; }

        [Display(Name = "Şifre")]
        [Required(ErrorMessage = "Şifre alanı boş geçilemez!")]
        [StringLength(30, ErrorMessage = "Şifre en fazla 30 karakter olabilir.")]
        [DataType(DataType.Password)]
        public string password { get; set; }
    }
}
