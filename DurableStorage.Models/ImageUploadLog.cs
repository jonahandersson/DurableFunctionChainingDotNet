using System;
using System.Collections.Generic;
using System.Text;

namespace DurableStorage.Models
{    public class ImageUploadLog
    {
        public Guid Id { get; set; }

        public string Message { get; set; }

        public string ImageName { get; set; }

        public string ImageUrl { get; set; }
    }
}
