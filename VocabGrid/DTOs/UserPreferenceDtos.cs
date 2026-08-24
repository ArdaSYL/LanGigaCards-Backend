using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace VocabGrid.DTOs;

public class ReplaceUserCategoriesDto
{
    [Required]
    public List<int> CategoryIds { get; set; } = new();
}

public class ReplaceUserLearningPurposesDto
{
    [JsonPropertyName("learningPurposeIds")]
    public List<int>? LearningPurposeIds { get; set; }

    [JsonPropertyName("purposeIds")]
    public List<int>? PurposeIds { get; set; }

    public List<int> ResolvedIds() =>
        (LearningPurposeIds ?? new List<int>())
            .Concat(PurposeIds ?? new List<int>())
            .Distinct()
            .ToList();
}
