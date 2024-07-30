using AutoMapper;
using Hangfire;
using MediatR;
using Para.Api.Jobs;
using Para.Base.Response;
using Para.Bussiness.Cqrs;
using Para.Bussiness.MessageQuakers.RabbitMQ.Abstract;
using Para.Bussiness.Notification;
using Para.Data.Domain;
using Para.Data.UnitOfWork;
using Para.Schema;

namespace Para.Bussiness.Command;

public class AccountCommandHandler :
    IRequestHandler<CreateAccountCommand, ApiResponse<AccountResponse>>,
    IRequestHandler<UpdateAccountCommand, ApiResponse>,
    IRequestHandler<DeleteAccountCommand, ApiResponse>
{
    private readonly IMessageProducer _messageProducer;
    private readonly IUnitOfWork unitOfWork;
    private readonly IMapper mapper;
    private readonly IJobEmailService _jobEmailService;
    public AccountCommandHandler(IJobEmailService jobEmailService, IMessageProducer messageProducer, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _messageProducer = messageProducer;
        this.unitOfWork = unitOfWork;
        this.mapper = mapper;
        _jobEmailService = jobEmailService;
    }

    public async Task<ApiResponse<AccountResponse>> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        var mapped = mapper.Map<AccountRequest, Account>(request.Request);
        mapped.OpenDate = DateTime.Now;
        mapped.Balance = 0;
        mapped.AccountNumber = new Random().Next(1000000, 9999999);
        mapped.IBAN = $"TR{mapped.AccountNumber}97925786{mapped.AccountNumber}01";
        var saved = await unitOfWork.AccountRepository.Insert(mapped);
        await unitOfWork.Complete();
        var customer = await unitOfWork.CustomerRepository.GetById(request.Request.CustomerId);
        _jobEmailService.DelayedJob(new NotificationModel
        {
            Content = $"Merhaba, {customer.FirstName}, Adiniza ${request.Request.CurrencyCode} doviz cinsi hesabiniz acilmistir.",
            Email = customer.Email,
            Subject = "Yeni hesap acilisi"
        });
        var response = mapper.Map<AccountResponse>(saved);
        return new ApiResponse<AccountResponse>(response);
    }

    public async Task<ApiResponse> Handle(UpdateAccountCommand request, CancellationToken cancellationToken)
    {
        var mapped = mapper.Map<AccountRequest, Account>(request.Request);
        mapped.Id = request.AccountId;
        unitOfWork.AccountRepository.Update(mapped);
        await unitOfWork.Complete();
        return new ApiResponse();
    }

    public async Task<ApiResponse> Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        await unitOfWork.AccountRepository.Delete(request.AccountId);
        await unitOfWork.Complete();
        return new ApiResponse();
    }
}