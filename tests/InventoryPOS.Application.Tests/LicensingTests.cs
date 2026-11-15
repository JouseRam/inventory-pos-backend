using InventoryPOS.Licensing;

namespace InventoryPOS.Application.Tests;

public class LicensingTests
{
    private static readonly (string Private, string Public) Keys = LicenseKey.CreateKeyPair();
    private static readonly DateOnly Today = new(2026, 11, 15);
    private static MachineCode Store => MachineCode.FromTraits("4c4c4544-0042-3510-8051-b4c04f4d4e32", "03000200-0400-0500-0006-000700080009", "PF3K2Z1A");

    [Fact]
    public void The_machine_code_reads_back_even_with_dashes_spaces_lowercase_and_confusable_letters()
    {
        var text = Store.ToString();
        Assert.Matches(@"^[0-9A-Z]{4}(-[0-9A-Z]{1,4}){6}$", text);
        Assert.True(MachineCode.TryParse(text.ToLowerInvariant().Replace("-", " "), out var parsed));
        Assert.Equal(text, parsed.ToString());
        // One wrong character is caught instead of producing a key for a computer that does not exist.
        var typo = (text[0] == 'A' ? 'B' : 'A') + text[1..];
        Assert.False(MachineCode.TryParse(typo, out _));
    }

    [Fact]
    public void A_key_activates_only_the_computer_it_was_issued_for()
    {
        var key = LicenseKey.Issue(Keys.Private, "Anna’s Beauty Supply", Store, Today);

        var check = LicenseKey.Check(key, Keys.Public, Store, Today);
        Assert.True(check.IsValid);
        Assert.Equal("Anna’s Beauty Supply", check.License!.Customer);

        // Windows reinstalled on the same computer: one trait changes, still the same computer.
        var reinstalled = MachineCode.FromTraits("9f1d0c2e-1111-2222-3333-444455556666", "03000200-0400-0500-0006-000700080009", "PF3K2Z1A");
        Assert.True(LicenseKey.Check(key, Keys.Public, reinstalled, Today).IsValid);

        // The folder copied to another computer.
        var other = MachineCode.FromTraits("aaaaaaaa-1111-2222-3333-444455556666", "11111111-2222-3333-4444-555566667777", "XK29PL0Q");
        Assert.Equal(LicenseStatus.OtherComputer, LicenseKey.Check(key, Keys.Public, other, Today).Status);
    }

    [Fact]
    public void Keys_that_were_edited_or_signed_by_someone_else_are_rejected()
    {
        var key = LicenseKey.Issue(Keys.Private, "Anna’s Beauty Supply", Store, Today);
        var parts = key.Split('.');
        var otherComputer = LicenseKey.Issue(Keys.Private, "Anna’s Beauty Supply",
            MachineCode.FromTraits("aaaaaaaa-1111-2222-3333-444455556666", "11111111-2222-3333-4444-555566667777", "XK29PL0Q"), Today).Split('.');

        // Someone swaps the data (another computer) keeping the signature.
        Assert.Equal(LicenseStatus.BadSignature, LicenseKey.Check($"{parts[0]}.{otherComputer[1]}.{parts[2]}", Keys.Public, Store, Today).Status);
        // A key made with another private key (someone else's key generator).
        var foreign = LicenseKey.CreateKeyPair();
        Assert.Equal(LicenseStatus.BadSignature, LicenseKey.Check(LicenseKey.Issue(foreign.PrivateKey, "X", Store, Today), Keys.Public, Store, Today).Status);
        Assert.Equal(LicenseStatus.Malformed, LicenseKey.Check("ABS1.hola", Keys.Public, Store, Today).Status);
        Assert.Equal(LicenseStatus.Missing, LicenseKey.Check("  ", Keys.Public, Store, Today).Status);
        // Line breaks from copying out of an email do not matter.
        Assert.True(LicenseKey.Check(key[..20] + "\r\n" + key[20..], Keys.Public, Store, Today).IsValid);
    }

    [Fact]
    public void A_key_with_an_end_date_stops_working_after_it()
    {
        var key = LicenseKey.Issue(Keys.Private, "Anna’s Beauty Supply", Store, Today, Today.AddDays(30));
        Assert.True(LicenseKey.Check(key, Keys.Public, Store, Today.AddDays(30)).IsValid);
        Assert.Equal(LicenseStatus.Expired, LicenseKey.Check(key, Keys.Public, Store, Today.AddDays(31)).Status);
    }

    [Fact]
    public void Placeholder_firmware_values_do_not_identify_a_computer()
    {
        Assert.False(MachineCode.IsUseful("To be filled by O.E.M."));
        Assert.False(MachineCode.IsUseful("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF"));
        Assert.False(MachineCode.IsUseful(null));
        // Two placeholders: the computer is known by its Windows id only, and that must match.
        var onlyWindows = MachineCode.FromTraits("4c4c4544-0042-3510-8051-b4c04f4d4e32", "00000000-0000-0000-0000-000000000000", "Default string");
        var key = LicenseKey.Issue(Keys.Private, "Anna’s", onlyWindows, Today);
        Assert.True(LicenseKey.Check(key, Keys.Public, onlyWindows, Today).IsValid);
        Assert.Throws<InvalidOperationException>(() => MachineCode.FromTraits(null, "", "None"));
    }
}
