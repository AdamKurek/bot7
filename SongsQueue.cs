using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bot7
{
    
    internal class SongsQueuee<T>:IEnumerable<T>
    {
        List<T> queue;
        public T DefaultSong { get; set; }
        object o = new object();

        public SongsQueuee(T @default)
        {
            queue = new List<T>();
            DefaultSong = @default;
        }

        public void enqueue(T v)
        {
            queue.Add(v);
        }

        public T Dequeue()
        {
            var ret = queue.FirstOrDefault(DefaultSong);
            queue.Remove(ret);
            return ret;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)queue).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)queue).GetEnumerator();
        }

        internal void insert(int v, T url)
        {
            queue.Insert(v, url);
        }

        internal void AppendEnd(IEnumerable<T> url)
        {
            queue.AddRange(url);
        }

        internal void AppendFront(IEnumerable<T> url)
        {
            var list = url.ToList();
            list.AddRange(queue);
            queue = list;
        }

        internal bool empty()
        {
            return queue.Count == 0 ? true : false;
        }

        internal void Clear()
        {
            queue.Clear();
        }

    }
}
