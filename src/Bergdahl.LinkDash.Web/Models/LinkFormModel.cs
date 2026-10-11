using System.ComponentModel.DataAnnotations;
using Bergdahl.LinkDash.Core.Models;
using Bergdahl.LinkDash.Core.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bergdahl.LinkDash.Web.Models;

public class LinkFormModel : IValidatableObject
{
    [Display(Name = "Group")]
    public Guid GroupId { get; set; }

    /// <summary>
    /// Empty when adding a new link.
    /// </summary>
    public Guid? LinkId { get; set; }

    /// <summary>
    /// Shown when editing, where the group cannot be changed. Not posted back as data.
    /// </summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>
    /// The groups to choose from when adding a link. Not posted back as data.
    /// </summary>
    [BindNever]
    public IReadOnlyList<LinkGroup> Groups { get; set; } = [];

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(LinkValidation.MaxTitleLength)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "URL is required.")]
    [StringLength(LinkValidation.MaxUrlLength)]
    [Display(Name = "URL")]
    public string Url
    {
        get => _url;
        // Trimmed on binding so length validation and the redisplayed form ignore surrounding whitespace.
        set => _url = value?.Trim() ?? string.Empty;
    }

    private string _url = "https://";

    [StringLength(LinkValidation.MaxDescriptionLength)]
    public string? Description { get; set; }

    [StringLength(LinkValidation.MaxIconLength)]
    public string? Icon { get; set; }

    public bool IsNew => LinkId is null;

    public LinkInput ToInput() => new(Title, Url, Description, Icon);

    public static LinkFormModel FromLink(LinkGroup group, Link link) => new()
    {
        GroupId = group.Id,
        GroupName = group.Name,
        LinkId = link.Id,
        Title = link.Title,
        Url = link.Url,
        Description = link.Description,
        Icon = link.Icon
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Url) && !LinkValidation.IsValidUrl(Url))
        {
            yield return new ValidationResult("Enter a full http:// or https:// address.", [nameof(Url)]);
        }

        if (!LinkValidation.IsValidIcon(Icon, allowFavicon: true))
        {
            yield return new ValidationResult($"Use a Material Design Icons name such as \"mdi:home\" or \"mdi:email\", or \"{LinkValidation.FaviconIcon}\".", [nameof(Icon)]);
        }
    }
}
