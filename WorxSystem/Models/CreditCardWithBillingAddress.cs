using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using RainWorx.FrameWorx.DTO;

namespace RainWorx.FrameWorx.MVC.Models {
	
	public class CreditCardWithBillingAddress {

		private CreditCard m_CreditCard;
		private Address m_BillingAddress;

		public CreditCardWithBillingAddress(CreditCard creditCard, Address billingAddress)
		{
            if (billingAddress != null) creditCard.Description = "****" + creditCard.LastFourDigits + ", " + billingAddress.Description;
			m_CreditCard = creditCard;
			m_BillingAddress = billingAddress;
		}

		public CreditCard CreditCard {
			get {
				return m_CreditCard;
			}
		}

		public Address BillingAddress {
			get {
				return m_BillingAddress;
			}
		}

	}

}