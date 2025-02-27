﻿using System;
using Newtonsoft.Json.Linq;
using sib_api_v3_sdk.Api;
using sib_api_v3_sdk.Model;
using System.Diagnostics;

namespace idefny.Services
{
	public class EmailSender
	{
		public static  void SendEmail(string senderEmail,string senderName,string receiverEmail, string receiverName, string subject,string message)
		{

            var apiInstance = new TransactionalEmailsApi();
           
            SendSmtpEmailSender sender = new SendSmtpEmailSender(senderName, senderEmail);
          
            SendSmtpEmailTo receiver1 = new SendSmtpEmailTo(receiverEmail, receiverName);
            List<SendSmtpEmailTo> To = new List<SendSmtpEmailTo>();
            To.Add(receiver1);
    
            string HtmlContent = null;
            string TextContent = message;
            
            try
            {
                var sendSmtpEmail = new SendSmtpEmail(sender, To, null, null, HtmlContent, TextContent, subject);
                CreateSmtpEmail result = apiInstance.SendTransacEmail(sendSmtpEmail);
             
                Console.WriteLine("brevo result:"+result.ToJson());
            
            }
            catch (Exception e)
            {
              
                Console.WriteLine("we have an excption:"+e.Message);
         
            }
        }
	}
}

