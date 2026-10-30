using JobHunter.Connectors.Abstractions.Applications;

namespace JobHunter.Connectors.Testing.Builders;

public sealed class ApplicationFormBuilder
{
    private readonly List<FormField> _fields = [];

    private ApplicationFormBuilder()
    {
    }

    public static ApplicationFormBuilder Default() => new();

    public ApplicationFormBuilder WithFullName(bool required = true) =>
        WithField(new FormField("full_name", "Nome completo", FieldKind.Text, FieldSemantic.FullName, required, [], 200, null));

    public ApplicationFormBuilder WithEmail(bool required = true) =>
        WithField(new FormField("email", "E-mail", FieldKind.Email, FieldSemantic.Email, required, [], 200, null));

    public ApplicationFormBuilder WithPhone(bool required = false) =>
        WithField(new FormField("phone", "Telefone", FieldKind.Phone, FieldSemantic.Phone, required, [], 30, null));

    public ApplicationFormBuilder WithResume(bool required = true) =>
        WithField(new FormField("resume", "Currículo", FieldKind.File, FieldSemantic.Resume, required, [], null, "PDF"));

    public ApplicationFormBuilder WithConsentTerms() =>
        WithField(new FormField("consent_terms", "Li e aceito os termos", FieldKind.Boolean, FieldSemantic.ConsentTerms, true, [], null, null));

    public ApplicationFormBuilder WithCustomQuestion(string label, bool required = false, int? maxLength = 1000) =>
        WithField(new FormField($"question_{_fields.Count + 1}", label, FieldKind.TextArea, FieldSemantic.Custom, required, [], maxLength, null));

    public ApplicationFormBuilder WithSingleChoice(string key, string label, params string[] options) =>
        WithField(new FormField(key, label, FieldKind.SingleChoice, FieldSemantic.Custom, false, [.. options.Select(option => new FieldOption(option, option))], null, null));

    public ApplicationFormBuilder WithField(FormField field)
    {
        _fields.Add(field);
        return this;
    }

    public ApplicationForm Build() => new(FormHasher.Compute(_fields), [.. _fields]);
}
