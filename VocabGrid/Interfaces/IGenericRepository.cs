using System.Linq.Expressions;

namespace VocabGrid.Interfaces
{
    public interface IGenericRepository<T> where T : class
    {
        Task<IEnumerable<T>> GetAllAsync();
        Task<T?> GetByIdAsync(int id);
        Task<T?> GetByIdAsync(object id);
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Henüz çalıştırılmamış sorgu. Yukarıdakilerin hepsi
        /// <see cref="IEnumerable{T}"/> döndürür, yani sonucu belleğe alır:
        /// tek bir <c>Where</c>'den sonrasında join, sıralama ve sayfalama
        /// C# tarafında yapılır ve şemadaki indeksler kullanılmaz.
        ///
        /// Bunu, birden fazla tabloyu birleştiren ya da sonucu sınırlayan
        /// okuma yolları için kullanın; tek satır getiren basit çağrılar
        /// mevcut metotlarla kalabilir.
        /// </summary>
        IQueryable<T> Query();
        Task AddAsync(T entity);
        void Update(T entity);
        void Delete(T entity);
    }
}
