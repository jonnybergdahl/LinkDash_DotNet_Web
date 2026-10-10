using System.ComponentModel.DataAnnotations;
using Bergdahl.LinkDash.Core.Services;

namespace Bergdahl.LinkDash.Web.Models;

public class GroupFormModel : IValidatableObject
{
    /// <summary>
    /// Empty when adding a new group.
    /// </summary>
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Group name is required.")]
    [StringLength(LinkValidation.MaxGroupNameLength)]
    [Display(Name = "Group name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(LinkValidation.MaxIconLength)]
    public string? Icon { get; set; }

    public bool IsNew => Id is null;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!LinkValidation.IsValidIcon(Icon))
        {
            yield return new ValidationResult("Use a Material Design Icons name such as \"mdi:home\" or \"mdi:email\".", [nameof(Icon)]);
        }
    }
}
