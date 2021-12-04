using System.Threading.Tasks;

namespace ExternalTools.DbscanImplementation.Eventing
{
    public interface IDbscanEventSubscriber<TR>
    {
        Task<TR> Subscribe();
    }
}