# Terraform Demo Infrastructure

This is an optional personal-subscription deployment. It is not applied by the demo and contains no reference to the APC subscription.

```powershell
az account set --subscription 04a39147-1a6b-4741-b535-77a1e4f91d7d
terraform init
terraform plan -var-file=terraform.tfvars
terraform apply -var-file=terraform.tfvars
```

The F1 plan is suitable only for a low-volume demonstration and may not support production requirements such as Always On, private networking or reliable background processing. Do not place API keys in Terraform state or `app_settings`; configure secrets out-of-band. Destroy the resource group after the demo if it is no longer required.
