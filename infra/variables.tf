variable "subscription_id" {
  type = string
}

variable "resource_group_name" {
  type    = string
  default = "rg-email-architecture-demo"
}

variable "location" {
  type    = string
  default = "australiaeast"
}

variable "storage_account_name" {
  type = string
}

variable "web_app_name" {
  type = string
}

variable "app_service_sku" {
  type    = string
  default = "F1"
}
