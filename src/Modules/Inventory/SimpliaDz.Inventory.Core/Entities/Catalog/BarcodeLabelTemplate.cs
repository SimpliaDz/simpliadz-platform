namespace SimpliaDz.Inventory.Core.Entities.Catalog;

public sealed class BarcodeLabelTemplate
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public decimal PageWidthMm { get; private set; }
    public decimal PageHeightMm { get; private set; }
    public decimal LabelWidthMm { get; private set; }
    public decimal LabelHeightMm { get; private set; }
    public decimal MarginMm { get; private set; }
    public decimal GapMm { get; private set; }
    public bool ShowName { get; private set; }
    public bool ShowSku { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private BarcodeLabelTemplate() { }

    public BarcodeLabelTemplate(string name, decimal pageWidthMm, decimal pageHeightMm,
        decimal labelWidthMm, decimal labelHeightMm, decimal marginMm, decimal gapMm,
        bool showName, bool showSku) =>
        Update(name, pageWidthMm, pageHeightMm, labelWidthMm, labelHeightMm,
            marginMm, gapMm, showName, showSku);

    public void Update(string name, decimal pageWidthMm, decimal pageHeightMm,
        decimal labelWidthMm, decimal labelHeightMm, decimal marginMm, decimal gapMm,
        bool showName, bool showSku)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new ArgumentException("Template name is required (maximum 100 characters).", nameof(name));
        if (pageWidthMm is < 50 or > 420 || pageHeightMm is < 50 or > 594 ||
            labelWidthMm is < 40 or > 200 || labelHeightMm is < 25 or > 200 ||
            marginMm is < 0 or > 50 || gapMm is < 0 or > 50 ||
            !TwoDecimals(pageWidthMm) || !TwoDecimals(pageHeightMm) ||
            !TwoDecimals(labelWidthMm) || !TwoDecimals(labelHeightMm) ||
            !TwoDecimals(marginMm) || !TwoDecimals(gapMm) ||
            labelWidthMm + marginMm * 2 > pageWidthMm ||
            labelHeightMm + marginMm * 2 > pageHeightMm)
            throw new ArgumentException("Page and label dimensions must fit in millimeters.");
        Name = name.Trim();
        PageWidthMm = pageWidthMm;
        PageHeightMm = pageHeightMm;
        LabelWidthMm = labelWidthMm;
        LabelHeightMm = labelHeightMm;
        MarginMm = marginMm;
        GapMm = gapMm;
        ShowName = showName;
        ShowSku = showSku;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate() { IsActive = false; UpdatedAt = DateTimeOffset.UtcNow; }
    public void Activate() { IsActive = true; UpdatedAt = DateTimeOffset.UtcNow; }

    private static bool TwoDecimals(decimal value) => decimal.Round(value, 2) == value;
}
