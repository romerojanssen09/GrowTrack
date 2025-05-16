using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Project_Creation.Models.Entities
{
    public class BOBusinessProfile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string ShopName { get; set; }

        [Required]
        [StringLength(100)]
        public string Description { get; set; }

        [Required]
        [StringLength(100)]
        public string BusinessBackgroundImgPath { get; set; }
    }
}
