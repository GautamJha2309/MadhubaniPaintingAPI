using FluentValidation;
using MadhubaniPaintingAPI.DTOs.Users;

namespace MadhubaniPaintingAPI.Validators
{
    public class UpdateUserValidator : AbstractValidator<UpdateUserRequest>
    {
        public UpdateUserValidator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty()
                .MaximumLength(100);
        }
    }
}
