﻿using NHibernate;
using NHibernate.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace PAK_Command_Editor.Repository
{
    public class Repository<T> : IRepository<T>, IDisposable
    {
        private readonly ISession session;

        public Repository(ISession session)
        {
            this.session = session;
        }

        public IQueryable<T> GetAll()
        {
            return session.Query<T>();
        }

        public IQueryable<T> Get(Expression<Func<T, bool>> predicate)
        {
            return GetAll().Where(predicate);
        }

        public IEnumerable<T> SaveOrUpdateAll(params T[] entities)
        {
            foreach (var entity in entities)
            {
                session.SaveOrUpdate(entity);
            }
            
            return entities;
        }

        public T SaveOrUpdate(T entity)
        {
            session.SaveOrUpdate(entity);

            return entity;
        }

        public void Delete(T entity)
        {
            session.Delete(entity);
        }

        #region IDisposable Implementation

        public void Dispose()
        {
            if (this.session == null) return;

            try
            {
                if (this.session.Transaction.IsActive)
                    this.session.Transaction.Commit();
            }
            catch (Exception)
            {
                if (this.session.Transaction.IsActive)
                    this.session.Transaction.Rollback();
            }
            finally
            {
                this.session.Close();
                this.session.Dispose();
            }
        }

        #endregion

    }
}
