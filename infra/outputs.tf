output "web_app_url" { value = "https://${azurerm_linux_web_app.api.default_hostname}" }
output "resource_group" { value = azurerm_resource_group.demo.name }
