using BusinessLayer.Abstract;
using DataAccessLayer.Abstract;
using EntityLayer.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Concrete
{
    class ReviewManager : IReviewService
    {
        IReviewDal _reviewDal;

        public ReviewManager(IReviewDal reviewDal)
        {
            _reviewDal = reviewDal;
        }

        public void TAdd(Review entity)
        {
           _reviewDal.Insert(entity);
        }

        public void TDelete(Review entity)
        {
           _reviewDal.Delete(entity);
        }

        public Review TGetById(int id)
        {
          return _reviewDal.GetById(id);
        }

        public List<Review> TGetList()
        {
            return _reviewDal.GetList();
        }

        public void TUpdate(Review entity)
        {
           _reviewDal.Update(entity);
        }
    }
}
