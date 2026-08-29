public class AllergenRecommendation
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string SuggestedName { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; 
    public DateTime SubmittedAt { get; set; }
}