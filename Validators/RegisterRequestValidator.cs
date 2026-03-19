using FluentValidation;
using MadhubaniPaintingAPI.DTOs;

namespace MadhubaniPaintingAPI.Validators
{
    public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
    {
        public RegisterRequestValidator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty()
                .MinimumLength(3);

            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress();

            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(8)
                .Matches("[A-Z]").WithMessage("Must contain uppercase")
                .Matches("[a-z]").WithMessage("Must contain lowercase")
                .Matches("[0-9]").WithMessage("Must contain number")
                .Matches("[^a-zA-Z0-9]").WithMessage("Must contain special char");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty()
                .Matches(@"^[6-9]\d{9}$")
                .WithMessage("Invalid Indian phone number");
        }
    }
}