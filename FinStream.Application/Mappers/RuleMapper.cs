// MAPPER FILE: Converts between SignalRule entity and DTO (Data Transfer Object).
// SignalRules define conditions that trigger alerts (e.g., "alert when price changes > 5%").
// This mapper handles the conversion between database entities and API DTOs.

// We need the DTOs for API responses and the Domain entities for database operations
using FinStream.Application.DTOs;
using FinStream.Domain.Entities;

namespace FinStream.Application.Mappers;

/// <summary>
/// Mapper for converting between SignalRule entity and DTO.
/// SignalRules are rule definitions stored in the database that traders create to trigger alerts.
/// Example: Trader creates rule "SPIKE" = "alert when price change > 5%"
/// </summary>
public static class RuleMapper
{
    /// <summary>
    /// Converts a SignalRule entity (from database) into a RuleDto (for API response).
    /// This is what the client receives when listing rules.
    /// </summary>
    /// <param name="rule">The rule entity from the database</param>
    /// <returns>A DTO ready to be sent to the client</returns>
    public static RuleDto ToDto(SignalRule rule) =>
        new(
            rule.Id,
            rule.Name,
            rule.ConditionType,
            rule.Threshold,
            rule.IsActive,
            rule.CreatedAt
        );

    /// <summary>
    /// Converts a CreateRuleDto (from client/API) into a SignalRule entity (for database).
    /// We normalize the condition type to uppercase during conversion.
    /// New rules are active by default when created.
    /// </summary>
    /// <param name="dto">The DTO data sent by the client when creating a rule</param>
    /// <returns>An entity ready to be saved to the database</returns>
    public static SignalRule ToEntity(CreateRuleDto dto) =>
        new()
        {
            Name = dto.Name,
            ConditionType = dto.ConditionType.ToUpperInvariant(),  // Normalize to uppercase (e.g., "pricetchange_gt" -> "PRICECHANGE_GT")
            Threshold = decimal.Parse(dto.Threshold),  // Convert string threshold to decimal (e.g., "5.0" -> 5.0m)
            IsActive = true  // New rules are active by default
        };
}